# The Hatchling's Journal by Damián Garro

![thumbnail](images/thumbnail.jpg)

This is **your** Journal. This Outer Wilds mod adds a [Custom Ship Log Mode](https://outerwildsmods.com/mods/customshiplogmodes/) added to your computer that lets you create and view your own Ship Log entries.

The entries are conveniently presented in a list format, similar to the vanilla Map Mode. You have the freedom to write names and descriptions for each entry, and you can easily edit, delete, or rearrange them as you please.

![entries-example](images/entries-example.gif)

With this mod, you can enhance your Outer Wilds adventure in various ways. Maybe you want to complement the vanilla Ship Log with personal notes, or create a more challenging and less hand-holding experience by relying solely on your own entries and ignoring the ones the game writes for you. Have you already beaten the game? You could use this tool to play the DLC if you haven't already, or with some of the [story mods](https://outerwildsmods.com/mods/?tag=story). Another option is to replay the game and immerse yourself in a role-playing journey by chronicling it from Hatchling's perspective. You can even use it for practical purposes like keeping school notes so you can study for that exam without leaving your spaceship or managing your grocery list, the possibilities are endless, you probably have better ideas.

The Hatchling's Journal unleashes the full power of [Épicas Album](https://outerwildsmods.com/mods/picasalbum/), enabling you to use your scout's uploaded snapshots (or memes) as the photos for your Journal's entries.

![epicas-example](images/epicas-example.gif)

Entries without photos are displayed with orange names, similar to the entries in the base game that represent "rumors" that weren't explored yet. However, you can interpret and use these entries however you like; there are no strict semantics to follow for your personal notes.

You can also add the "More to Explore" icon to the entries, along with the  *"There's more to explore here."* orange text item to the description. This can serve as a visual bookmark to highlight entries that require specific attention or further exploration.

To create or edit entries, please note that the input fields (for the entry name and description) require keyboard usage. If you're using a gamepad, you'll need to set it aside temporarily when interacting with these fields. These fields also allow some usual commands like copy and paste, moving the caret or selecting text.

![input-example](images/input-example.gif)

## Savefile Management

Changes made to your Journal are saved to disk when you exit the mode by either switching to another Ship Log mode or exiting the computer. The save files are stored in the "saves" folder within the mod directory. Each Outer Wilds profile has its own separate Journal, represented by a "(profile name).json" (**JSON** file). To ensure the safety of your Journal, it's recommended to back up this file (as well as any images from Épicas Album that you use), as you should do with any file you don't want to lose in general.

⚠️Please note that uninstalling the mod (not disabling it) will delete the entire mod folder, including all the saved files. Exercise caution when uninstalling the mod to avoid losing your Journal.⚠️

Additionally, you may find "(profile name).json.old" (**OLD** files) in the "saves" folder. These files serve as automatic backups created during the first save in each loop before overwriting with changes. If needed, you can restore a previous version of your Journal by deleting the main JSON file and removing the ".old" extension from the corresponding OLD file.

Manually editing the savefile is not recommended unless you fully understand its structure. If the game fails to read the savefile, the Journal would start empty again. But don't worry, if that happens, a ".corrupted" extension is added to the previous savefile to prevent it from being overwritten by the new fresh Journal.  In such cases, you can attempt to fix any mistakes by editing the "(profile name).json.corrupted" (**CORRUPTED** file), or seek assistance by opening an issue on [GitHub repository](https://github.com/dgarroDC/Journal/issues) or joining the [Outer Wilds Modding discord server](https://discord.gg/CRfxGWJG24). Restoring the OLD file could also be a viable option.
