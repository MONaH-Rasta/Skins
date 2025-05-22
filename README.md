# Skins

Oxide plugin for Rust. Change workshop skins of items easily.

This plugin will allow players to change skin for items in easiest way. Just type skin command, move item to container and move back with skin.

## WARNING! READ FIRST (FAQ)

* Plugin **does NOT add skins automatically**. You have to add them to configuration yourself or with API / help of another plugin.
* **Stack Extended** may break the plugin. For example, when you have custom stack on an item that is used by Skins.
* Plugin only **sends a number** of the skin to the client. If your client **doesn't load** skins but items appear, that means that it's the **Rust client** (your) **issue**. Could be anything from Steam to your download speeds and Rust issues.
* Skins are **not** stored on the server. Only the IDs (numbers from config).
* **Download Timed Out**. Try to set `graphics.itemskintimeout` to something like 300 and it should fix all your problems with it, else most likely a Steam issue.

## Permissions

* `skins.use` - Permission for basic plugin usage. (Opening the box to change skins)
* `skins.admin` - Permission for advanced admin-only plugin usage. (Add or remove skins)

## Commands

* `skin` - Main and the only command of the plugin. Can be changed in configuration. Syntax:
* `skin show` - Show skins
* `skin get` - Get Skin ID of the item
* `skin purgecache (shortname)` - Purge skins cache by shortname (or empty to purge all)
* `skin remove (Shortname) (Skin ID)` - Remove a skin
* `skin add (Shortname) (Skin ID)` - Add a skin

## Configuration

### Default Configuration

```json
{
  "Commands": [
    "skin",
    "skins"
  ],
  "Skins": [
    {
      "Item Shortname": "shortname",
      "Permission": "",
      "Skins": [
        0
      ]
    }
  ],
  "Container Panel Name": "generic",
  "Container Capacity": 36,
  "UI": {
    "Background Color": "0.18 0.28 0.36",
    "Background Anchors": {
      "Anchor Min X": "1.0",
      "Anchor Min Y": "1.0",
      "Anchor Max X": "1.0",
      "Anchor Max Y": "1.0"
    },
    "Background Offsets": {
      "Offset Min X": "-300",
      "Offset Min Y": "-100",
      "Offset Max X": "0",
      "Offset Max Y": "0"
    },
    "Left Button Text": "<size=36><</size>",
    "Left Button Color": "0.11 0.51 0.83",
    "Left Button Anchors": {
      "Anchor Min X": "0.025",
      "Anchor Min Y": "0.05",
      "Anchor Max X": "0.325",
      "Anchor Max Y": "0.95"
    },
    "Center Button Text": "<size=36>Page: {page}</size>",
    "Center Button Color": "0.11 0.51 0.83",
    "Center Button Anchors": {
      "Anchor Min X": "0.350",
      "Anchor Min Y": "0.05",
      "Anchor Max X": "0.650",
      "Anchor Max Y": "0.95"
    },
    "Right Button Text": "<size=36>></size>",
    "Right Button Color": "0.11 0.51 0.83",
    "Right Button Anchors": {
      "Anchor Min X": "0.675",
      "Anchor Min Y": "0.05",
      "Anchor Max X": "0.975",
      "Anchor Max Y": "0.95"
    }
  }
}
```

## Language

### English

```json
{
  "Not Allowed": "You don't have permission to use this command.",
  "Cannot Use": "I'm sorry, you cannot use that right now.",
  "Help": "Command usage:\nskin show - Show skins.\nskin get - Get Skin ID of the item.\nskin purgecache (shortname) - Purge skins cache by shortname (or empty to purge all)",
  "Admin Help": "Admin command usage:\nskin remove (Shortname) (Skin ID) [Permission] - Remove a skin.\nskin add (Shortname) (Skin ID) [Permission] - Add a skin.",
  "Skin Get Format": "{shortname}'s skin: {id}.",
  "Skin Get No Item": "Please, hold the needed item.",
  "Incorrect Skin": "You have entered an incorrect skin.",
  "Skin Already Exists": "This skin already exists on this item.",
  "Skin Does Not Exist": "This skin does not exist.",
  "Skin Added": "Skin was successfully added.",
  "Skin Removed": "Skin was removed."
}
```

## For Developers

### Hooks

```csharp
// Called when player tries to open skins menu
// Return bool to override, null to leave it default
private object CanUseSkins(string playerId)

// Called when an item with a new skin has been removed from box
private void OnItemSkinChanged(BasePlayer player, Item item)

// Called prior to searching for skins
// List is empty, use to add custom skins
private void OnSkinsFetch(BasePlayer player, ItemDefinition info, List<ulong> skins)

// Called after adding custom and configuration skins
// Use to log, process, replace and remove skins
private void OnSkinsFetched(BasePlayer player, ItemDefinition info, List<ulong> skins)

// Called after finding skins for current page
private void OnSkinsPage(BasePlayer player, ItemDefinition info, List<ulong> skins, int page)
```

### API

```csharp
// Call to close the skin box
void SkinsClose(BasePlayer player)

// Purge cache for a specific item. Use null or empty string to purge all items
void PurgeCache(ulong id, string shortname)
```

### Additional comments

In case you want to close skinbox and deny opening it, first close the box with API call. Only then return false in `CanUseSkins`, because if you close the box AFTER you start returning false in `CanUseSkins`, you can achieve unstable behaviour.
