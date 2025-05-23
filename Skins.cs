﻿//#define DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Skins", "misticos", "2.2.3")]
    [Description("Change workshop skins of items easily")]
    class Skins : RustPlugin
    {
        #region Variables

        private static Skins _ins;

        private readonly Dictionary<ulong, ContainerController> _controllers = new();

        private readonly Dictionary<ItemContainerId, ContainerController> _controllersPerContainer =
            new();

        private readonly HashSet<ItemContainerId> _itemAttachmentContainers = new();

        private const string PermissionUse = "skins.use";
        private const string PermissionAdmin = "skins.admin";

        private const string CommandDefault = "skins.skin";

        #endregion

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands")]
            public string[] Commands = { "skin", "skins" };

            [JsonProperty(PropertyName = "Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<SkinItem> Skins = new() { new SkinItem() };

            [JsonIgnore] public Dictionary<string, List<SkinItem>> IndexedSkins = new();

            [JsonProperty(PropertyName = "Container Panel Name")]
            public string Panel = "generic";

            [JsonProperty(PropertyName = "Container Capacity")]
            public int Capacity = 36;

            [JsonProperty(PropertyName = "UI")] public UIConfiguration UI = new();

            public class SkinItem
            {
                [JsonProperty(PropertyName = "Item Shortname")]
                // ReSharper disable once MemberCanBePrivate.Local
                public string Shortname = "shortname";

                [JsonProperty(PropertyName = "Permission")]
                public string Permission = "";

                [JsonProperty(PropertyName = "Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<ulong> Skins = new() { 0 };

                public static IEnumerable<SkinItem> Find(IPlayer player, string shortname)
                {
                    if (!_ins._config.IndexedSkins.TryGetValue(shortname, out List<SkinItem> items))
                        yield break;

                    foreach (SkinItem item in items)
                    {
                        if (!item.CanUse(player))
                            continue;

                        yield return item;
                    }
                }

                public bool CanUse(IPlayer player) => player == null ||
                                                      string.IsNullOrEmpty(Permission) ||
                                                      player.HasPermission(Permission);
            }

            public class UIConfiguration
            {
                [JsonProperty(PropertyName = "Background Color")]
                public string BackgroundColor = "0.18 0.28 0.36";

                [JsonProperty(PropertyName = "Background Anchors")]
                public Anchors BackgroundAnchors = new() { AnchorMinX = "1.0", AnchorMinY = "1.0", AnchorMaxX = "1.0", AnchorMaxY = "1.0" };

                [JsonProperty(PropertyName = "Background Offsets")]
                public Offsets BackgroundOffsets = new() { OffsetMinX = "-300", OffsetMinY = "-100", OffsetMaxX = "0", OffsetMaxY = "0" };

                [JsonProperty(PropertyName = "Left Button Text")]
                public string LeftText = "<size=36><</size>";

                [JsonProperty(PropertyName = "Left Button Color")]
                public string LeftColor = "0.11 0.51 0.83";

                [JsonProperty(PropertyName = "Left Button Anchors")]
                public Anchors LeftAnchors = new() { AnchorMinX = "0.025", AnchorMinY = "0.05", AnchorMaxX = "0.325", AnchorMaxY = "0.95" };

                [JsonProperty(PropertyName = "Center Button Text")]
                public string CenterText = "<size=36>Page: {page}</size>";

                [JsonProperty(PropertyName = "Center Button Color")]
                public string CenterColor = "0.11 0.51 0.83";

                [JsonProperty(PropertyName = "Center Button Anchors")]
                public Anchors CenterAnchors = new() { AnchorMinX = "0.350", AnchorMinY = "0.05", AnchorMaxX = "0.650", AnchorMaxY = "0.95" };

                [JsonProperty(PropertyName = "Right Button Text")]
                public string RightText = "<size=36>></size>";

                [JsonProperty(PropertyName = "Right Button Color")]
                public string RightColor = "0.11 0.51 0.83";

                [JsonProperty(PropertyName = "Right Button Anchors")]
                public Anchors RightAnchors = new() { AnchorMinX = "0.675", AnchorMinY = "0.05", AnchorMaxX = "0.975", AnchorMaxY = "0.95" };

                [JsonIgnore] public string ParsedUI;

                [JsonIgnore] public int IndexPagePrevious, IndexPageCurrent, IndexPageNext;

                public class Anchors
                {
                    [JsonProperty(PropertyName = "Anchor Min X")]
                    public string AnchorMinX = "0.0";

                    [JsonProperty(PropertyName = "Anchor Min Y")]
                    public string AnchorMinY = "0.0";

                    [JsonProperty(PropertyName = "Anchor Max X")]
                    public string AnchorMaxX = "1.0";

                    [JsonProperty(PropertyName = "Anchor Max Y")]
                    public string AnchorMaxY = "1.0";

                    [JsonIgnore] public string AnchorMin => $"{AnchorMinX} {AnchorMinY}";

                    [JsonIgnore] public string AnchorMax => $"{AnchorMaxX} {AnchorMaxY}";
                }

                public class Offsets
                {
                    [JsonProperty(PropertyName = "Offset Min X")]
                    public string OffsetMinX = "0";

                    [JsonProperty(PropertyName = "Offset Min Y")]
                    public string OffsetMinY = "0";

                    [JsonProperty(PropertyName = "Offset Max X")]
                    public string OffsetMaxX = "100";

                    [JsonProperty(PropertyName = "Offset Max Y")]
                    public string OffsetMaxY = "100";

                    [JsonIgnore] public string OffsetMin => $"{OffsetMinX} {OffsetMinY}";

                    [JsonIgnore] public string OffsetMax => $"{OffsetMaxX} {OffsetMaxY}";
                }
            }

            public void IndexSkins()
            {
                IndexedSkins.Clear();

                foreach (SkinItem item in Skins)
                {
                    if (!string.IsNullOrEmpty(item.Permission) && !_ins.permission.PermissionExists(item.Permission))
                        _ins.permission.RegisterPermission(item.Permission, _ins);

                    if (!IndexedSkins.TryGetValue(item.Shortname, out List<SkinItem> items))
                        items = IndexedSkins[item.Shortname] = new List<SkinItem>();

                    items.Add(item);
                }
            }
        }

        protected override void LoadConfig()
        {
            _ins = this; // REEE that I have to do this tbh

            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();

                GenerateUI();
                _config.IndexSkins();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Not Allowed", "You don't have permission to use this command." },
                { "Cannot Use", "I'm sorry, you cannot use that right now." },
                {
                    "Help", "Command usage:\n" +
                            "skin show - Show skins.\n" +
                            "skin get - Get Skin ID of the item.\n" +
                            "skin purgecache (shortname) - Purge skins cache by shortname (or empty to purge all)"
                },
                {
                    "Admin Help", "Admin command usage:\n" +
                                  "skin remove (Shortname) (Skin ID) [Permission] - Remove a skin.\n" +
                                  "skin add (Shortname) (Skin ID) [Permission] - Add a skin."
                },
                { "Skin Get Format", "{shortname}'s skin: {id}." },
                { "Skin Get No Item", "Please, hold the needed item." },
                { "Incorrect Skin", "You have entered an incorrect skin." },
                { "Skin Already Exists", "This skin already exists on this item." },
                { "Skin Does Not Exist", "This skin does not exist." },
                { "Skin Added", "Skin was successfully added." },
                { "Skin Removed", "Skin was removed." }
            }, this);
        }

        private void Init()
        {
            _ins = this;

            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);

            GenerateUI();
        }

        private void GenerateUI()
        {
            const string pagePrevious = "{pagePrevious}";
            const string pageCurrent = "{page}";
            const string pageNext = "{pageNext}";

            CuiElementContainer elements = new CuiElementContainer();

            CuiElement background = new CuiElement
            {
                Name = "Skins.Background",
                Parent = "Overlay",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = _ins._config.UI.BackgroundColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = _ins._config.UI.BackgroundAnchors.AnchorMin,
                        AnchorMax = _ins._config.UI.BackgroundAnchors.AnchorMax,
                        OffsetMin = _ins._config.UI.BackgroundOffsets.OffsetMin,
                        OffsetMax = _ins._config.UI.BackgroundOffsets.OffsetMax
                    }
                },
                FadeOut = 0.5f
            };

            CuiElement left = new CuiElement
            {
                Name = "Skins.Left",
                Parent = background.Name,
                Components =
                {
                    new CuiButtonComponent
                    {
                        Close = background.Name,
                        Command = $"{CommandDefault} _tech-update {pagePrevious}",
                        Color = _ins._config.UI.LeftColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = _ins._config.UI.LeftAnchors.AnchorMin,
                        AnchorMax = _ins._config.UI.LeftAnchors.AnchorMax
                    }
                },
                FadeOut = 0.5f
            };

            CuiElement leftText = new CuiElement
            {
                Name = "Skins.Left.Text",
                Parent = left.Name,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _ins._config.UI.LeftText,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                },
                FadeOut = 0.5f
            };

            CuiElement center = new CuiElement
            {
                Name = "Skins.Center",
                Parent = background.Name,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = _ins._config.UI.CenterColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = _ins._config.UI.CenterAnchors.AnchorMin,
                        AnchorMax = _ins._config.UI.CenterAnchors.AnchorMax
                    }
                },
                FadeOut = 0.5f
            };

            CuiElement centerText = new CuiElement
            {
                Name = "Skins.Center.Text",
                Parent = center.Name,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _ins._config.UI.CenterText,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                },
                FadeOut = 0.5f
            };

            CuiElement right = new CuiElement
            {
                Name = "Skins.Right",
                Parent = background.Name,
                Components =
                {
                    new CuiButtonComponent
                    {
                        Close = background.Name,
                        Command = $"{CommandDefault} _tech-update {pageNext}",
                        Color = _ins._config.UI.RightColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = _ins._config.UI.RightAnchors.AnchorMin,
                        AnchorMax = _ins._config.UI.RightAnchors.AnchorMax
                    }
                },
                FadeOut = 0.5f
            };

            CuiElement rightText = new CuiElement
            {
                Name = "Skins.Right.Text",
                Parent = right.Name,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _ins._config.UI.RightText,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                },
                FadeOut = 0.5f
            };

            elements.Add(background);
            elements.Add(left);
            elements.Add(leftText);
            elements.Add(center);
            elements.Add(centerText);
            elements.Add(right);
            elements.Add(rightText);

            _config.UI.ParsedUI = elements.ToJson();

            _config.UI.IndexPagePrevious = _config.UI.ParsedUI.LastIndexOf(pagePrevious, StringComparison.Ordinal);
            _config.UI.ParsedUI = _config.UI.ParsedUI.Remove(_config.UI.IndexPagePrevious, pagePrevious.Length);

            _config.UI.IndexPageCurrent = _config.UI.ParsedUI.LastIndexOf(pageCurrent, StringComparison.Ordinal);
            _config.UI.ParsedUI = _config.UI.ParsedUI.Remove(_config.UI.IndexPageCurrent, pageCurrent.Length);

            _config.UI.IndexPageNext = _config.UI.ParsedUI.LastIndexOf(pageNext, StringComparison.Ordinal);
            _config.UI.ParsedUI = _config.UI.ParsedUI.Remove(_config.UI.IndexPageNext, pageNext.Length);
        }

        private void OnServerInitialized()
        {
            foreach (string shortname in _config.IndexedSkins.Keys)
            {
                if (ItemManager.FindItemDefinition(shortname))
                    continue;

                PrintWarning(
                    $"Item with shortname \"{shortname}\" does not exist. Please review your Skins configuration.");
            }

            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                OnPlayerConnected(BasePlayer.activePlayerList[i]);
            }

            AddCovalenceCommand(_config.Commands, nameof(CommandSkin));
            AddCovalenceCommand(CommandDefault, nameof(CommandSkin));
        }

        private void Unload()
        {
            foreach (KeyValuePair<ulong, ContainerController> controller in _controllers)
                controller.Value.Destroy();

            _ins = null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (_controllers.ContainsKey(player.userID))
                return;

            _controllers.Add(player.userID, new ContainerController(player)); // lol
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (!_controllers.Remove(player.userID, out ContainerController container))
                return;

            container.Destroy();
        }

        #region Working With Containers

        private void OnItemSplit(Item item, int amount)
        {
            if (item.parentItem != null || item.parent == null)
                return;

            if (!_controllersPerContainer.TryGetValue(item.parent.uid, out ContainerController container))
                return;

#if DEBUG
            Puts($"OnItemSplit: {item.info.shortname} ({item.amount}x, slot {item.position}); {amount}x");
#endif

            Item main = container.Container.GetSlot(0);
            if (main == null)
            {
#if DEBUG
                Puts("Main item is null");
#endif
                return;
            }

            NextFrame(() =>
            {
                if (main.uid != item.uid) // Ignore main item because it's amount will be changed
                    main.amount -= amount;

                container.UpdateContent(0);
            });
        }

        private void OnItemAddedToContainer(ItemContainer itemContainer, Item item)
        {
            if (item.parentItem != null)
                return;

            BasePlayer player = itemContainer.GetOwnerPlayer();
            if (player != null)
                return;

            if (!_controllersPerContainer.TryGetValue(itemContainer.uid, out ContainerController container))
                return;

#if DEBUG
            Puts($"OnItemAddedToContainer: {item.info.shortname} (slot {item.position})");
#endif

            if (itemContainer.itemList.Count != 1)
            {
                item.position = -1;
                item.parent.itemList.Remove(item);
                item.parent = null;
                container.GiveItemBack();
                container.Clear();
                item.parent = container.Container;
                item.parent.itemList.Add(item);
            }

            item.position = 0;
            container.StoreContent(item);
            container.UpdateContent(0);
        }

        private void OnItemRemovedFromContainer(ItemContainer itemContainer, Item item)
        {
            if (item.parentItem != null)
                return;

            BasePlayer player = itemContainer.GetOwnerPlayer();
            if (player != null)
                return;

            if (!_controllersPerContainer.TryGetValue(itemContainer.uid, out ContainerController container))
                return;

#if DEBUG
            Puts($"OnItemRemovedFromContainer: {item.info.shortname} (slot {item.position})");
#endif

            container.OnItemTaken(item);

            Interface.CallHook("OnItemSkinChanged", player, item);

            container.Clear();
        }

        private void OnPlayerLootEnd(PlayerLoot loot)
        {
            BasePlayer player = loot.gameObject.GetComponent<BasePlayer>();
            if (player != loot.entitySource)
                return;

#if DEBUG
            Puts("OnLootEntityEnd: Closing container");
#endif

            if (!_controllers.TryGetValue(player.userID, out ContainerController container))
                return;

            container.Close();
        }

        private object CanLootPlayer(BasePlayer looter, BasePlayer target)
        {
            if (looter != target)
                return null;

            if (!_controllers.TryGetValue(looter.userID, out ContainerController container) || !container.IsOpened)
                return null;

            return true;
        }

        private object CanMoveItem(Item item, PlayerInventory _, ItemContainerId targetContainerId, int slot,
            int amount)
        {
            if (_itemAttachmentContainers.Contains(targetContainerId))
            {
#if DEBUG
                Puts("// CanMoveItem: Preventing attachments abuse");
#endif
                return false;
            }

            if (!_controllersPerContainer.TryGetValue(targetContainerId, out ContainerController containerTo) &&
                (item.parent == null || !_controllersPerContainer.ContainsKey(item.parent.uid)))
                return null;

#if DEBUG
            Puts(
                $"CanMoveItem: {item.info.shortname} ({item.amount}) from {item.parent?.uid.Value ?? 0} to {targetContainerId} in {slot} ({amount})");
#endif

            if (item.parent?.uid == targetContainerId)
            {
#if DEBUG
                Puts("// CanMoveItem: Preventing same containers");
#endif

                return false;
            }

            return CanMoveItemTo(containerTo, item, slot, amount);
        }

        #region Minor helpers

        private object CanMoveItemTo(ContainerController controller, Item _, int slot, int _1)
        {
            Item targetItem = controller?.Container?.GetSlot(slot);
            if (targetItem != null)
            {
                // Give target item back
                controller.GiveItemBack(targetItem);
                controller.Clear();
            }

            return null;
        }

        #endregion

        #endregion

        #endregion

        #region Commands

        private void CommandSkin(IPlayer player, string _, string[] args)
        {
            if (!CanUse(player))
            {
#if DEBUG
                Puts("Not allowed");
#endif

                player.Reply(GetMsg("Not Allowed", player.Id));
                return;
            }

            BasePlayer basePlayer = player.Object as BasePlayer;
            bool isPlayer = basePlayer != null;
            bool isAdmin = player.IsServer || CanUseAdmin(player);

            if (args.Length == 0)
                args = new[] { isPlayer ? "show" : string.Empty }; // :P strange yeah


#if DEBUG
            Puts($"Arguments: {string.Join(" ", args)}");
#endif

            switch (args[0].ToLower())
            {
                case "_tech-update":
                {
                    if (!isPlayer)
                        break;

                    if (args.Length != 2 || !int.TryParse(args[1], out int page))
                        break;

                    if (!_controllers.TryGetValue(basePlayer.userID, out ContainerController container))
                        break;

                    container.UpdateContent(page);
                    break;
                }

                case "purgecache":
                case "pc":
                {
                    if (!isPlayer)
                        break;

                    if (!_controllers.TryGetValue(basePlayer.userID, out ContainerController container))
                        break;

                    container.TotalSkinsCache.Clear();
                    break;
                }

                case "show":
                case "s":
                {
                    if (!isPlayer)
                    {
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    if (!_controllers.TryGetValue(basePlayer.userID, out ContainerController container) || !container.CanShow())
                    {
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    basePlayer.Invoke(container.Show, 0.5f);
                    break;
                }

                case "remove":
                case "delete":
                case "r":
                case "d":
                {
                    if (args.Length < 3)
                        goto default;

                    if (!isAdmin)
                    {
                        player.Reply(GetMsg("Not Allowed", player.Id));
                        break;
                    }

                    string shortname = args[1];
                    if (!ulong.TryParse(args[2], out ulong skin))
                    {
                        player.Reply(GetMsg("Incorrect Skin", player.Id));
                        break;
                    }

                    string perm = null;
                    if (args.Length == 4)
                        perm = args[3];

                    LoadConfig();

                    IEnumerable<Configuration.SkinItem> skinData = Configuration.SkinItem.Find(null, shortname)
                        .Where(x => perm == null || x.Permission == perm);

                    var skinDataList = Configuration.SkinItem.Find(null, shortname)
                        .Where(x => perm == null || x.Permission == perm)
                        .ToList(); // Materialize to List to avoid multiple enumeration

                    if (!skinDataList.Any())
                    {
                        player.Reply(GetMsg("Skin Does Not Exist", player.Id));
                        break;
                    }

                    foreach (Configuration.SkinItem data in skinDataList)
                        data.Skins.Remove(skin);

                    player.Reply(GetMsg("Skin Removed", player.Id));

                    SaveConfig();
                    break;
                }

                case "add":
                case "a":
                {
                    if (args.Length < 3)
                        goto default;

                    if (!isAdmin)
                    {
                        player.Reply(GetMsg("Not Allowed", player.Id));
                        break;
                    }

                    string shortname = args[1];
                    if (!ulong.TryParse(args[2], out ulong skin))
                    {
                        player.Reply(GetMsg("Incorrect Skin", player.Id));
                        break;
                    }

                    string perm = null;
                    if (args.Length == 4)
                        perm = args[3];

                    LoadConfig();

                    Configuration.SkinItem skinData = Configuration.SkinItem.Find(null, shortname)
                        .FirstOrDefault(x => perm == null || x.Permission == perm);

                    if (skinData == null)
                    {
                        _config.Skins.Add(new Configuration.SkinItem
                        {
                            Permission = perm ?? string.Empty,
                            Shortname = shortname,
                            Skins = new List<ulong> { skin }
                        });

                        _config.IndexSkins();
                        player.Reply(GetMsg("Skin Added", player.Id));
                    }
                    else
                    {
                        if (skinData.Skins.Contains(skin))
                            player.Reply(GetMsg("Skin Already Exists", player.Id));
                        else
                        {
                            skinData.Skins.Add(skin);
                            player.Reply(GetMsg("Skin Added", player.Id));
                        }
                    }

                    SaveConfig();
                    break;
                }

                case "get":
                case "g":
                {
                    if (!isPlayer)
                    {
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    Item item = basePlayer.GetActiveItem();
                    if (item == null || !item.IsValid())
                    {
                        player.Reply(GetMsg("Skin Get No Item", player.Id));
                        break;
                    }

                    player.Reply(GetMsg("Skin Get Format", player.Id).Replace("{shortname}", item.info.shortname)
                        .Replace("{id}", item.skin.ToString()));

                    break;
                }

                default: // "help" and all other args
                {
                    player.Reply(GetMsg("Help", player.Id));
                    if (isAdmin)
                        player.Reply(GetMsg("Admin Help", player.Id));

                    break;
                }
            }
        }

        #endregion

        #region API

        [HookMethod(nameof(SkinsClose))]
        private void SkinsClose(BasePlayer player)
        {
            if (player == null)
                return;

            if (!_controllers.TryGetValue(player.userID, out ContainerController container))
                return;

            container.Close();
        }

        [HookMethod(nameof(PurgeCache))]
        private void PurgeCache(ulong id, string shortname)
        {
            if (!_controllers.TryGetValue(id, out ContainerController container))
                return;

            if (string.IsNullOrEmpty(shortname))
            {
                container.TotalSkinsCache.Clear();
            }
            else
            {
                container.TotalSkinsCache.Remove(shortname);
            }
        }

        #endregion

        #region Controller

        private class ContainerController
        {
            /*
             * Basic tips:
             * Item with slot 0: Player's skin item
             */

            public BasePlayer Owner;
            public ItemContainer Container;
            public bool IsOpened;

            public Dictionary<string, List<ulong>> TotalSkinsCache = new();

            private readonly List<Item> _storedContent;
            private Magazine _storedMagazine;

            public ContainerController(BasePlayer player)
            {
                Owner = player;
                _storedContent = new List<Item>();

                Container = new ItemContainer
                {
                    entityOwner = Owner,
                    capacity = _ins._config.Capacity,
                    isServer = true,
                    allowedContents = ItemContainer.ContentsType.Generic
                };

                Container.GiveUID();

                _ins._controllersPerContainer[Container.uid] = this;
            }

            #region UI

            private void DestroyUI()
            {
                CuiHelper.DestroyUi(Owner, "Skins.Background");
            }

            private void DrawUI(int page)
            {
#if DEBUG
                _ins.Puts("Drawing UI");
#endif

                CuiHelper.AddUi(Owner, _ins._config.UI.ParsedUI
                    .Insert(_ins._config.UI.IndexPageNext, (page + 1).ToString())
                    .Insert(_ins._config.UI.IndexPageCurrent, page.ToString())
                    .Insert(_ins._config.UI.IndexPagePrevious, (page - 1).ToString()));
            }

            #endregion

            public void Close()
            {
#if DEBUG
                _ins.Puts("Closing container");
#endif

                DestroyUI();
                GiveItemBack();
                Clear();

                IsOpened = false;
            }

            public void Show()
            {
#if DEBUG
                _ins.Puts($"Showing container. UID: {Container.uid}");
#endif

                if (!CanUse())
                    return;

                IsOpened = true;
                UpdateContent(0);

                PlayerLoot loot = Owner.inventory.loot;

                loot.Clear();
                loot.PositionChecks = false;
                loot.entitySource = Owner;
                loot.itemSource = null;
                loot.AddContainer(Container);
                loot.SendImmediate();

                Owner.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", Owner), _ins._config.Panel);
            }

            #region Can Show

            public bool CanShow()
            {
                return CanShow(Owner);
            }

            private static bool CanShow(BasePlayer player)
            {
                return player != null && !player.IsDead() && !player.IsWounded() && !player.IsIncapacitated();
            }

            #endregion

            private void AddItemContainer(Item item)
            {
                if (item?.contents == null)
                    return;

                if (!item.contents.uid.IsValid)
                    return;

                _ins._itemAttachmentContainers.Add(item.contents.uid);
            }

            public void GiveItemBack(Item itemOverride = null)
            {
                if (!IsValid())
                    return;

#if DEBUG
                _ins.Puts("Trying to give item back..");
#endif

                Item item = itemOverride ?? Container.GetSlot(0);
                if (item == null)
                {
#if DEBUG
                    _ins.Puts("Invalid item");
#endif

                    return;
                }

                MoveItem(item, Owner.inventory.containerMain);
                OnItemTaken(item);
            }

            public void OnItemTaken(Item item)
            {
                if (item?.contents != null)
                    _ins._itemAttachmentContainers.Remove(item.contents.uid);

                SetupContent(item);
            }

            public void SetupContent(Item destination)
            {
#if DEBUG
                _ins.Puts("Setting up content for an item");
#endif
                if (destination == null)
                {
#if DEBUG
                    _ins.Puts("Destination is null!");
#endif

                    return;
                }

                if (_storedMagazine != null)
                {
                    (destination.GetHeldEntity() as BaseProjectile)?.primaryMagazine?.Load(_storedMagazine);
                    _storedMagazine = null;
                }

                List<Item> contents = destination.contents?.itemList;
                if (contents == null)
                {
#if DEBUG
                    _ins.Puts("// Contents null");
#endif

                    return;
                }

                for (int i = _storedContent.Count - 1; i >= 0; i--)
                {
                    Item item = _storedContent[i];
                    item.parent = destination.contents;
                    item.RemoveFromWorld();

                    _storedContent.RemoveAt(i);
                    contents.Add(item);

                    item.MarkDirty();
                    foreach (ItemMod itemMod in item.info.itemMods)
                        itemMod.OnParentChanged(item);
                }

                _storedContent.Clear();
            }

            public void StoreContent(Item source)
            {
#if DEBUG
                _ins.Puts("Removing content for an item");
#endif

                List<Item> contents = source.contents?.itemList;
                if (contents != null)
                {
                    for (int i = contents.Count - 1; i >= 0; i--)
                    {
                        Item item = contents[i];
                        item.parent = null;
                        contents.RemoveAt(i);
                        _storedContent.Add(item);
                    }
                }

                BaseProjectile.Magazine magazine = (source.GetHeldEntity() as BaseProjectile)?.primaryMagazine;
                _storedMagazine = magazine?.Save();

                if (magazine != null) // Just in case so they won't be able to take out the ammo
                    magazine.contents = 0;
            }

            public void Clear()
            {
#if DEBUG
                _ins.Puts("Clearing container");
#endif

                for (int i = Container.itemList.Count - 1; i >= 0; i--)
                {
                    RemoveItem(Container.itemList[i]);
                }

                Container.itemList.Clear();
                Container.MarkDirty();
            }

            public void Destroy()
            {
                Close();
                Container.Kill();
            }

            public void UpdateContent(int page)
            {
                if (!IsValid())
                {
#if DEBUG
                    _ins.Puts("// Invalid container");
#endif

                    return;
                }

                Item source = Container.GetSlot(0);
                if (source == null)
                {
#if DEBUG
                    _ins.Puts("// Source item is null");
#endif

                    return;
                }

                if (!source.uid.IsValid || !source.IsValid() || source.amount <= 0)
                {
#if DEBUG
                    _ins.Puts("// Invalid item that was removed. Player may have tried to dupe something");
#endif

                    return;
                }

                List<ulong> skins = Pool.Get<List<ulong>>();
                try
                {
                    // Cache or get total skins available for user

                    if (!TotalSkinsCache.TryGetValue(source.info.shortname, out List<ulong> totalSkins))
                    {
                        // Fetch custom skins

                        List<ulong> newSkins = new List<ulong>();

                        Interface.CallHook("OnSkinsFetch", Owner, source.info, newSkins);

                        TotalSkinsCache[source.info.shortname] = totalSkins = newSkins.Concat(Configuration.SkinItem
                            .Find(Owner.IPlayer, source.info.shortname)
                            .SelectMany(x => x.Skins)).Distinct().ToList();

                        Interface.CallHook("OnSkinsFetched", Owner, source.info, newSkins);
                    }

                    // Page checks

                    int perPage = Container.capacity - 1;
                    int maxPage = (totalSkins.Count - 1) / perPage;

                    if (page < 0)
                        page = 0;

                    if (page > maxPage)
                        page = maxPage;

                    // Grab skins and skip some offset

                    foreach (ulong skin in totalSkins.Skip(perPage * page).Take(perPage))
                        skins.Add(skin);

                    Interface.CallHook("OnSkinsPage", Owner, source.info, skins, page);

                    Container.itemList.Remove(source);
                    for (int i = 0; i < source.info.itemMods.Length; i++)
                    {
                        ItemMod itemMod = source.info.itemMods[i];
                        itemMod.OnParentChanged(source);
                    }

#if DEBUG
                    _ins.Puts($"Updating content. Page: {page}");
#endif

                    Clear();

                    MoveItem(source, Container);
                    DestroyUI();
                    DrawUI(page);

                    for (int i = 0; i < skins.Count; i++)
                    {
                        Item duplicate = GetDuplicateItem(source, skins[i]);
                        MoveItem(duplicate, Container, i + 1);
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref skins);
                }
            }

            private bool IsValid() => Owner == null || Container?.itemList != null;

            private bool CanUse()
            {
                object result = Interface.CallHook("CanUseSkins", Owner.IPlayer.Id);
                if (result is not bool)
                    return true;

#if DEBUG
                _ins.Puts($"Hook result: {result}");
#endif

                return (bool)result;
            }

            #region Working with items

            private Item GetDuplicateItem(Item item, ulong skin)
            {
                Item duplicate = ItemManager.Create(item.info, item.amount, skin);
                if (item.hasCondition)
                {
                    duplicate._maxCondition = item._maxCondition;
                    duplicate._condition = item._condition;
                }

                if (item.contents != null)
                {
                    duplicate.contents.capacity = item.contents.capacity;
                }

                BaseProjectile projectile = duplicate.GetHeldEntity() as BaseProjectile;
                if (projectile != null)
                    projectile.primaryMagazine.contents = 0;

                return duplicate;
            }

            private void MoveItem(Item item, ItemContainer container, int slot = 0)
            {
                while (container.SlotTaken(item, slot) && container.capacity > slot)
                    slot++;

                if (container.IsFull() || container.SlotTaken(item, slot))
                {
#if DEBUG
                    _ins.Puts("Container is full, dropping item");
#endif

                    item.Drop(Owner.transform.position, Vector3.up);
                    return;
                }

                item.parent?.itemList?.Remove(item);

                item.RemoveFromWorld();

                item.position = slot;
                item.parent = container;

                container.itemList.Add(item);
                item.MarkDirty();

                for (int i = 0; i < item.info.itemMods.Length; i++)
                {
                    item.info.itemMods[i].OnParentChanged(item);
                }

                if (container == Container)
                    AddItemContainer(item);
            }

            private void RemoveItem(Item item)
            {
                if (item.uid.IsValid && Net.sv != null)
                {
                    Net.sv.ReturnUID(item.uid.Value);
                    item.uid = default(ItemId);
                }

                if (item.contents != null)
                {
                    for (int i = item.contents.itemList.Count - 1; i >= 0; i--)
                    {
                        RemoveItem(item.contents.itemList[i]);
                    }

                    item.contents = null;
                }

                item.RemoveFromWorld();

                item.parent = null;

                BaseEntity heldEntity = item.GetHeldEntity();
                if (heldEntity != null && heldEntity.IsValid() && !heldEntity.IsDestroyed)
                    heldEntity.Kill();
            }

            #endregion
        }

        #endregion

        #region Helpers

        private bool CanUse(IPlayer player) => player.HasPermission(PermissionUse);

        private bool CanUseAdmin(IPlayer player) => player.HasPermission(PermissionAdmin);

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}