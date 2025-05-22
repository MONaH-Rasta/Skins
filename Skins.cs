using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Oxide.Plugins
{
    [Info("Skins", "Iv Misticos", "2.0.3")]
    [Description("Change workshop skins of items easily")]
    class Skins : RustPlugin
    {
        #region Variables
        
        private static List<ContainerController> _controllers;

        private const string PermissionUse = "skins.use";
        private const string PermissionAdmin = "skins.admin";
        
        #endregion

        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Workshop", NullValueHandling = NullValueHandling.Ignore)]
            public Dictionary<string, List<ulong>> OldSkins = null;

            [JsonProperty(PropertyName = "Command")]
            public string Command = "skin";

            [JsonProperty(PropertyName = "Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<SkinItem> Skins = new List<SkinItem> {new SkinItem()};

            [JsonProperty(PropertyName = "Container Panel Name")]
            public string Panel = "generic";

            [JsonProperty(PropertyName = "Container Capacity")]
            public int Capacity = 36;

            [JsonProperty(PropertyName = "UI")]
            public UIConfiguration UI = new UIConfiguration();

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;

            public class SkinItem
            {
                [JsonProperty(PropertyName = "Item Shortname")]
                // ReSharper disable once MemberCanBePrivate.Local
                public string Shortname = "shortname";

                [JsonProperty(PropertyName = "Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<ulong> Skins = new List<ulong> {0};

                public static SkinItem Find(string shortname)
                {
                    for (var i = 0; i < _config.Skins.Count; i++)
                    {
                        var item = _config.Skins[i];
                        if (item.Shortname == shortname)
                            return item;
                    }

                    return null;
                }
            }

            public class UIConfiguration
            {
                [JsonProperty(PropertyName = "Background Color")]
                public string BackgroundColor = "0.18 0.28 0.36";

                [JsonProperty(PropertyName = "Background Anchors")]
                public Anchors BackgroundAnchors = new Anchors
                    {AnchorMinX = "1.0", AnchorMinY = "1.0", AnchorMaxX = "1.0", AnchorMaxY = "1.0"};

                [JsonProperty(PropertyName = "Background Offsets")]
                public Offsets BackgroundOffsets = new Offsets
                    {OffsetMinX = "-300", OffsetMinY = "-100", OffsetMaxX = "0", OffsetMaxY = "0"};
                
                [JsonProperty(PropertyName = "Left Button Text")]
                public string LeftText = "<size=36><</size>";
                
                [JsonProperty(PropertyName = "Left Button Color")]
                public string LeftColor = "0.11 0.51 0.83";

                [JsonProperty(PropertyName = "Left Button Anchors")]
                public Anchors LeftAnchors = new Anchors
                    {AnchorMinX = "0.025", AnchorMinY = "0.05", AnchorMaxX = "0.325", AnchorMaxY = "0.95"};
                
                [JsonProperty(PropertyName = "Center Button Text")]
                public string CenterText = "<size=36>Page: {page}</size>";
                
                [JsonProperty(PropertyName = "Center Button Color")]
                public string CenterColor = "0.11 0.51 0.83";

                [JsonProperty(PropertyName = "Center Button Anchors")]
                public Anchors CenterAnchors = new Anchors
                    {AnchorMinX = "0.350", AnchorMinY = "0.05", AnchorMaxX = "0.650", AnchorMaxY = "0.95"};
                
                [JsonProperty(PropertyName = "Right Button Text")]
                public string RightText = "<size=36>></size>";
                
                [JsonProperty(PropertyName = "Right Button Color")]
                public string RightColor = "0.11 0.51 0.83";

                [JsonProperty(PropertyName = "Right Button Anchors")]
                public Anchors RightAnchors = new Anchors
                    {AnchorMinX = "0.675", AnchorMinY = "0.05", AnchorMaxX = "0.975", AnchorMaxY = "0.95"};

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

                    [JsonIgnore]
                    public string AnchorMin => $"{AnchorMinX} {AnchorMinY}";

                    [JsonIgnore]
                    public string AnchorMax => $"{AnchorMaxX} {AnchorMaxY}";
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

                    [JsonIgnore]
                    public string OffsetMin => $"{OffsetMinX} {OffsetMinY}";

                    [JsonIgnore]
                    public string OffsetMax => $"{OffsetMaxX} {OffsetMaxY}";
                }
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
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
                { "Not Allowed", "You don't have permission to use this command" },
                { "Cannot Use", "I'm sorry, you cannot use that right now" },
                { "Help", "Command usage:\n" +
                          "skin show - Show skins\n" +
                          "skin get - Get Skin ID of the item" },
                { "Admin Help", "Admin command usage:\n" +
                                "skin show - Show skins\n" +
                                "skin get - Get Skin ID of the item\n" +
                                "skin remove (Shortname) (Skin ID) - Remove a skin\n" +
                                "skin add (Shortname) (Skin ID) - Add a skin" },
                { "Skin Get Format", "{shortname}'s skin: {id}" },
                { "Skin Get No Item", "Please, hold the needed item" },
                { "Incorrect Skin", "You have entered an incorrect skin" },
                { "Skin Already Exists", "This skin already exists on this item" },
                { "Skin Does Not Exist", "This skin does not exist" },
                { "Skin Added", "Skin was successfully added" },
                { "Skin Removed", "Skin was removed" }
            }, this);
        }

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);
            
            _controllers = new List<ContainerController>();

            if (_config.OldSkins == null)
                return;
            
            foreach (var kvp in _config.OldSkins)
            {
                var skinItem = Configuration.SkinItem.Find(kvp.Key);
                if (skinItem == null)
                {
                    _config.Skins.Add(new Configuration.SkinItem {Shortname = kvp.Key, Skins = kvp.Value});
                    continue;
                }
                
                skinItem.Skins.AddRange(kvp.Value);
            }

            _config.OldSkins = null;
            SaveConfig();
        }

        private void OnServerInitialized()
        {
            for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                var player = BasePlayer.activePlayerList[i];
                OnPlayerInit(player);
            }

            AddCovalenceCommand(_config.Command, nameof(CommandSkin));
        }

        private void Unload()
        {
            for (var i = _controllers.Count - 1; i >= 0; i--)
            {
                var container = _controllers[i];
                container.Destroy();
                
                _controllers.RemoveAt(i);
            }

            _controllers = null;
            _config = null;
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (ContainerController.FindIndex(player) != -1)
                return;
            
            _controllers.Add(new ContainerController(player)); // lol
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            var index = ContainerController.FindIndex(player);
            if (index == -1)
                return;
            
            var container = _controllers[index];
            container.Destroy();
            
            _controllers.RemoveAt(index);
        }

        #region Working With Containers

        private void OnItemAddedToContainer(ItemContainer itemContainer, Item item)
        {
            if (item.parentItem != null)
                return;

            var player = itemContainer.GetOwnerPlayer();
            var container = ContainerController.Find(itemContainer);
            if (container == null || player != null)
                return;
            
            PrintDebug($"OnItemAddedToContainer {item.info.shortname} {item.position}");

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
            container.RemoveContent(item);
            container.UpdateContent(0);
        }

        private void OnItemRemovedFromContainer(ItemContainer itemContainer, Item item)
        {
            if (item.parentItem != null)
                return;
            
            var player = itemContainer.entityOwner as BasePlayer;
            var container = ContainerController.Find(itemContainer);
            if (container == null || player == null || container.Owner != player)
            {
                return;
            }

            PrintDebug($"OnItemRemovedFromContainer {item.info.shortname} {item.position}");
            
            container.SetupContent(item);
            container.Clear();
        }

        private void OnPlayerLootEnd(PlayerLoot loot)
        {
            var player = loot.gameObject.GetComponent<BasePlayer>();
            if (player != loot.entitySource)
                return;
            
            PrintDebug("OnLootEntityEnd");
            
            var container = ContainerController.Find(player);
            container?.Close();
        }

        private object CanLootPlayer(BasePlayer looter, Object target)
        {
            if (looter != target)
                return null;

            var container = ContainerController.Find(looter);
            if (container == null || !container.IsOpened)
                return null;

            return true;
        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainerId, int slot, int amount)
        {
            PrintDebug(
                $"Move {item.info.shortname} ({item.amount}) from {item.parent?.uid ?? 0} to {targetContainerId} in {slot} ({amount})");

            if (item.parent?.uid == targetContainerId)
            {
                PrintDebug("Move Ignoring same containers");
                return null;
            }

            var containerFrom = ContainerController.Find(item.parent);
            var containerTo = ContainerController.Find(targetContainerId);
            return CanMoveItemFrom(containerFrom, item, playerLoot, slot, amount) ??
                   CanMoveItemTo(containerTo, item, playerLoot, slot, amount);
        }

        #region Minor helpers

        private object CanMoveItemFrom(ContainerController controller, Item item, PlayerInventory playerLoot, int slot, int amount)
        {
            if (controller == null || item.amount == amount)
                return null; // That's all legal, calm down
            
            PrintDebug("Changing main item amount");
            var mainItem = controller.Container.GetSlot(0);
            if (mainItem == null)
                return false; // Just cancel. Illegal!
            
            mainItem.amount = item.amount - amount; // Change main item amount and refresh content.
                
            // Next frame because else it will change already existing item which may result in a wrong amount of the item :(
            NextFrame(() => controller.UpdateContent(0));
            return null; // That's legal, we'll do everything
        }

        private object CanMoveItemTo(ContainerController controller, Item item, PlayerInventory playerLoot, int slot, int amount)
        {
            var targetItem = controller?.Container.GetSlot(slot);
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

        [HookMethod(nameof(SkinsClose))]
        private void SkinsClose(BasePlayer player)
        {
            if (player == null)
                return;
            
            ContainerController.Find(player)?.Close();
        }
        
        #endregion

        #region Commands

        private void CommandSkin(IPlayer player, string command, string[] args)
        {
            PrintDebug("Executed Skin command");
            
            if (!CanUse(player))
            {
                PrintDebug("Not allowed");
                player.Reply(GetMsg("Not Allowed", player.Id));
                return;
            }

            if (args.Length == 0)
                args = new[] {"show"}; // :P strange yeah

            var isAdmin = player.IsServer || CanUseAdmin(player);
            var basePlayer = player.Object as BasePlayer;
            var isPlayer = basePlayer != null;
            
            PrintDebug($"Arguments: {string.Join(" ", args)}");
            PrintDebug($"Is Admin: {isAdmin} : Is Player: {isPlayer}");
            
            switch (args[0].ToLower())
            {
                case "_tech-update":
                {
                    int page;
                    if (args.Length != 2 || !isPlayer || !int.TryParse(args[1], out page))
                        break;

                    var container = ContainerController.Find(basePlayer);

                    container?.UpdateContent(page);
                    break;
                }
                    
                case "show":
                case "s":
                {
                    if (!isPlayer)
                    {
                        PrintDebug("Not a player");
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    var container = ContainerController.Find(basePlayer);
                    if (container == null || !container.CanShow())
                    {
                        PrintDebug("Cannot show container or container not found");
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    timer.Once(0.5f, () => container.Show());
                    break;
                }

                case "add":
                case "a":
                {
                    if (args.Length != 3)
                        goto default;

                    if (!isAdmin)
                    {
                        PrintDebug("Not an admin");
                        player.Reply(GetMsg("Not Allowed", player.Id));
                        break;
                    }

                    var shortname = args[1];
                    ulong skin;
                    if (!ulong.TryParse(args[2], out skin))
                    {
                        PrintDebug("Invalid skin");
                        player.Reply(GetMsg("Incorrect Skin", player.Id));
                        break;
                    }
                    
                    LoadConfig();

                    var skinData = Configuration.SkinItem.Find(shortname);
                    if (skinData == null)
                    {
                        skinData = new Configuration.SkinItem {Shortname = shortname};
                        _config.Skins.Add(skinData);
                    }

                    if (skinData.Skins.Contains(skin))
                    {
                        PrintDebug("Skin already exists");
                        player.Reply(GetMsg("Skin Already Exists", player.Id));
                        break;
                    }
                    
                    skinData.Skins.Add(skin);
                    player.Reply(GetMsg("Skin Added", player.Id));
                    
                    SaveConfig();
                    PrintDebug("Added skin");
                    break;
                }

                case "remove":
                case "delete":
                case "r":
                case "d":
                {
                    if (args.Length != 3)
                        goto default;

                    if (!isAdmin)
                    {
                        PrintDebug("Not an admin");
                        player.Reply(GetMsg("Not Allowed", player.Id));
                        break;
                    }

                    var shortname = args[1];
                    ulong skin;
                    if (!ulong.TryParse(args[2], out skin))
                    {
                        PrintDebug("Invalid skin");
                        player.Reply(GetMsg("Incorrect Skin", player.Id));
                        break;
                    }
                    
                    LoadConfig();

                    var skinData = Configuration.SkinItem.Find(shortname);
                    int index;
                    if (skinData == null || (index = skinData.Skins.IndexOf(skin)) == -1)
                    {
                        PrintDebug("Skin doesnt exist");
                        player.Reply(GetMsg("Skin Does Not Exist", player.Id));
                        break;
                    }
                    
                    skinData.Skins.RemoveAt(index);
                    player.Reply(GetMsg("Skin Removed", player.Id));
                    
                    SaveConfig();
                    PrintDebug("Removed skin");
                    break;
                }

                case "get":
                case "g":
                {
                    if (!isPlayer)
                    {
                        PrintDebug("Not a player");
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    var item = basePlayer.GetActiveItem();
                    if (item == null || !item.IsValid())
                    {
                        PrintDebug("Invalid item");
                        player.Reply(GetMsg("Skin Get No Item", player.Id));
                        break;
                    }

                    player.Reply(GetMsg("Skin Get Format", player.Id).Replace("{shortname}", item.info.shortname)
                        .Replace("{id}", item.skin.ToString()));
                    
                    break;
                }

                default: // and "help", and all other args
                {
                    PrintDebug("Unknown command");
                    player.Reply(GetMsg(isAdmin ? "Admin Help" : "Help", player.Id));
                    break;
                }
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
            public bool IsOpened = false;

            private List<Item> _storedContent;

            #region Search

            // ReSharper disable once SuggestBaseTypeForParameter
            public static int FindIndex(BasePlayer player)
            {
                if (!CanShow(player))
                    goto none;
                
                for (var i = 0; i < _controllers.Count; i++)
                {
                    if (_controllers[i].Owner == player)
                    {
                        return i;
                    }
                }

                none:
                return -1;
            }

            public static ContainerController Find(BasePlayer player)
            {
                var index = FindIndex(player);
                return index == -1 ? null : _controllers[index];
            }

            // ReSharper disable once SuggestBaseTypeForParameter
            private static int FindIndex(ItemContainer container)
            {
                if (container == null)
                    goto none;
                
                for (var i = 0; i < _controllers.Count; i++)
                {
                    if (_controllers[i].Container == container)
                    {
                        return i;
                    }
                }

                none:
                return -1;
            }

            public static ContainerController Find(ItemContainer container)
            {
                var index = FindIndex(container);
                return index == -1 ? null : _controllers[index];
            }

            // ReSharper disable once SuggestBaseTypeForParameter
            private static int FindIndex(uint id)
            {
                for (var i = 0; i < _controllers.Count; i++)
                {
                    if (_controllers[i].Container.uid == id)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public static ContainerController Find(uint id)
            {
                var index = FindIndex(id);
                return index == -1 ? null : _controllers[index];
            }
            
            #endregion

            public ContainerController(BasePlayer player)
            {
                Owner = player;
                _storedContent = new List<Item>();
                
                Container = new ItemContainer
                {
                    entityOwner = Owner,
                    capacity = _config.Capacity,
                    isServer = true,
                    allowedContents = ItemContainer.ContentsType.Generic
                };
                
                Container.GiveUID();
            }
            
            #region UI

            private void DestroyUI()
            {
                PrintDebug("Destroying UI");
                CuiHelper.DestroyUi(Owner, "Skins.Background");
            }

            private void DrawUI(int page)
            {
                PrintDebug("Drawing UI");
                var elements = new CuiElementContainer();

                var background = new CuiElement
                {
                    Name = "Skins.Background",
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = _config.UI.BackgroundColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = _config.UI.BackgroundAnchors.AnchorMin,
                            AnchorMax = _config.UI.BackgroundAnchors.AnchorMax,
                            OffsetMin = _config.UI.BackgroundOffsets.OffsetMin,
                            OffsetMax = _config.UI.BackgroundOffsets.OffsetMax
                        }
                    },
                    FadeOut = 0.5f
                };

                var left = new CuiElement
                {
                    Name = "Skins.Left",
                    Parent = background.Name,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Close = background.Name,
                            Command = $"{_config.Command} _tech-update {page - 1}",
                            Color = _config.UI.LeftColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = _config.UI.LeftAnchors.AnchorMin,
                            AnchorMax = _config.UI.LeftAnchors.AnchorMax
                        }
                    },
                    FadeOut = 0.5f
                };

                var leftText = new CuiElement
                {
                    Name = "Skins.Left.Text",
                    Parent = left.Name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = _config.UI.LeftText,
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

                var center = new CuiElement
                {
                    Name = "Skins.Center",
                    Parent = background.Name,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = _config.UI.CenterColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = _config.UI.CenterAnchors.AnchorMin,
                            AnchorMax = _config.UI.CenterAnchors.AnchorMax
                        }
                    },
                    FadeOut = 0.5f
                };

                var centerText = new CuiElement
                {
                    Name = "Skins.Center.Text",
                    Parent = center.Name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = _config.UI.CenterText.Replace("{page}", $"{page + 1}"),
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

                var right = new CuiElement
                {
                    Name = "Skins.Right",
                    Parent = background.Name,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Close = background.Name,
                            Command = $"{_config.Command} _tech-update {page + 1}",
                            Color = _config.UI.RightColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = _config.UI.RightAnchors.AnchorMin,
                            AnchorMax = _config.UI.RightAnchors.AnchorMax
                        }
                    },
                    FadeOut = 0.5f
                };

                var rightText = new CuiElement
                {
                    Name = "Skins.Right.Text",
                    Parent = right.Name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = _config.UI.RightText,
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

                CuiHelper.AddUi(Owner, elements);
            }
            
            #endregion

            public void Close()
            {
                PrintDebug("Closing container");
                
                GiveItemBack();
                Clear();
                DestroyUI();
                
                IsOpened = false;
            }

            public void Show()
            {
                PrintDebug($"Started container Show. Container UID: {Container.uid}");

                if (!CanUse())
                    return;
                
                IsOpened = true;
                UpdateContent(0);
                
                var loot = Owner.inventory.loot;
                
                loot.Clear();
                loot.PositionChecks = false;
                loot.entitySource = Owner;
                loot.itemSource = null;
                loot.AddContainer(Container);
                loot.SendImmediate();
                
                Owner.ClientRPCPlayer(null, Owner, "RPC_OpenLootPanel", _config.Panel);
            }
            
            #region Can Show

            public bool CanShow()
            {
                return CanShow(Owner);
            }

            private static bool CanShow(BaseCombatEntity player)
            {
                return player != null && !player.IsDead();
            }
            
            #endregion

            public void GiveItemBack(Item itemOverride = null)
            {
                if (!IsValid())
                    return;
                
                PrintDebug("Trying to give item back..");

                var item = itemOverride ?? Container.GetSlot(0);
                if (item == null)
                {
                    PrintDebug("Invalid item");
                    return;
                }

                MoveItem(item, Owner.inventory.containerMain, true);
                SetupContent(item);
            }

            public void SetupContent(Item destination)
            {
                PrintDebug("Setting up content");
                
                var contents = destination.contents?.itemList;
                if (contents == null)
                {
                    PrintDebug("Contents null");
                    return;
                }
                
                for (var i = _storedContent.Count - 1; i >= 0; i--)
                {
                    var item = _storedContent[i];
                    item.parent = destination.contents;
                    item.RemoveFromWorld();
                    
                    _storedContent.RemoveAt(i);
                    contents.Add(item);
                    
                    item.MarkDirty();
                    foreach (var itemMod in item.info.itemMods)
                        itemMod.OnParentChanged(item);
                }
            }

            public void RemoveContent(Item source)
            {
                PrintDebug("Removing content");
                
                var contents = source.contents?.itemList;
                if (contents == null)
                {
                    PrintDebug("Contents null");
                    return;
                }

                for (var i = contents.Count - 1; i >= 0; i--)
                {
                    var item = contents[i];
                    item.parent = null;
                    contents.RemoveAt(i);
                    _storedContent.Add(item);
                }
            }

            public void Clear()
            {
                PrintDebug("Clearing container");
                
                for (var i = 0; i < Container.itemList.Count; i++)
                {
                    var item = Container.itemList[i];
                    RemoveItem(item);
                }
                
                Container.itemList.Clear();
                Container.MarkDirty();
            }

            public void Destroy()
            {
                Close();
                Container.Kill();
                
                PrintDebug("Destroyed container");
            }

            public void UpdateContent(int page)
            {
                if (!IsValid())
                {
                    PrintDebug("Invalid container");
                    return;
                }
                
                var source = Container.GetSlot(0);
                if (source == null)
                {
                    PrintDebug("Source item is null");
                    return;
                }

                var skinData = Configuration.SkinItem.Find(source.info.shortname);
                if (skinData == null)
                    return;

                var skins = skinData.Skins;
                var perPage = Container.capacity - 1;

                if (page < 0)
                    page = 0;
                
                var offset = perPage * page;
                if (offset > skinData.Skins.Count)
                {
                    page--;
                    offset -= perPage;
                }

                Container.itemList.Remove(source);
                for (var i = 0; i < source.info.itemMods.Length; i++)
                {
                    var itemMod = source.info.itemMods[i];
                    itemMod.OnParentChanged(source);
                }

                PrintDebug($"Updating content with {page} page");
                Clear();
                
                MoveItem(source, Container, false, 0);
                DestroyUI();
                DrawUI(page);

                var slot = 1;
                for (var i = 0; i < skins.Count; i++)
                {
                    if (slot >= Container.capacity)
                        break;

                    if (offset > i)
                        continue;

                    var skin = skins[i];
                    var duplicate = GetDuplicateItem(source, skin);
                    
                    MoveItem(duplicate, Container, false, slot++);
                }
            }

            private bool IsValid() => Owner == null || Container?.itemList != null;

            private bool CanUse()
            {
                var result = Interface.CallHook("CanUseSkins", Owner.IPlayer.Id);
                if (!(result is bool))
                    return true;
                
                PrintDebug($"Hook result: {result}");
                return (bool) result;
            }
            
            #region Working with items

            private Item GetDuplicateItem(Item item, ulong skin)
            {
                PrintDebug($"Getting duplicate for {item.info.shortname}..");

                var duplicate = ItemManager.Create(item.info, item.amount, skin);
                if (item.hasCondition)
                {
                    PrintDebug("Setting condition");
                    duplicate._maxCondition = item._maxCondition;
                    duplicate._condition = item._condition;
                }

                if (item.contents != null)
                {
                    PrintDebug("Setting contents");
                    duplicate.contents.capacity = item.contents.capacity;
                }

                var projectile = item.GetHeldEntity() as BaseProjectile;
                var projectileDuplicate = duplicate.GetHeldEntity() as BaseProjectile;
                if (projectile == null || projectileDuplicate == null)
                    return duplicate;
                
                projectileDuplicate.primaryMagazine = projectile.primaryMagazine;
                return duplicate;
            }

            private void MoveItem(Item item, ItemContainer container, bool stacking, int slot = -1)
            {
                if (container.IsFull())
                {
                    PrintDebug("Container full, dropping item");
                    item.Drop(Owner.transform.position, Vector3.zero);
                    return;
                }

                var parent = item.parent;
                parent?.itemList?.Remove(item);

                item.RemoveFromWorld();

                item.position = slot;
                item.parent = container;

                container.itemList.Add(item);
                item.MarkDirty();

                for (var i = 0; i < item.info.itemMods.Length; i++)
                {
                    var mod = item.info.itemMods[i];
                    mod.OnParentChanged(item);
                }
            }

            private void RemoveItem(Item item)
            {
                if (item.uid > 0U && Network.Net.sv != null)
                {
                    Network.Net.sv.ReturnUID(item.uid);
                    item.uid = 0U;
                }
                
                if (item.contents != null)
                {
                    for (var i = 0; i < item.contents.itemList.Count; i++)
                    {
                        var content = item.contents.itemList[i];
                        RemoveItem(content);
                    }

                    item.contents = null;
                }
                
                item.RemoveFromWorld();
                var parent = item.parent;
                if (parent != null)
                {
                    parent.itemList.Remove(item);
                    item.parent = null;
                }
                
                var heldEntity = item.GetHeldEntity();
                if (heldEntity != null && heldEntity.IsValid())
                    heldEntity.Kill();
            }
            
            #endregion
        }
        
        #endregion
        
        #region Helpers

        private bool CanUse(IPlayer player) => player.HasPermission(PermissionUse);

        private bool CanUseAdmin(IPlayer player) => player.HasPermission(PermissionAdmin);

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        private static void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug(message);
        }

        #endregion
    }
}