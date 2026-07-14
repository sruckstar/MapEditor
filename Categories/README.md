# Object categories

The object picker is split into categories instead of one flat list. Each category is one plain-text
file, and **the files are the source of truth** — the game reads them at startup, so editing a file is
all it takes to change what shows up where.

## Installing

Copy the `Categories` folder next to the other Map Editor data files:

```
scripts\MapEditor\Categories\Props\*.txt      <- ships with the mod (from ObjectList.ini)
scripts\MapEditor\Categories\Vehicles\*.txt   <- written on first run
scripts\MapEditor\Categories\Peds\*.txt       <- written on first run
```

Vehicle and ped categories cannot ship, because both lists are built at runtime from the
ScriptHookVDotNet enums. The mod writes them out the first time it runs and reads them back like any
other category file, so they are just as editable.

If `Props` is missing the picker still works — it falls back to a single **All** category, i.e. the
old flat list.

## File format

```
# Vegetation & Nature
# One model name per line. Lines starting with # are ignored.
prop_tree_birch_01
prop_bush_lrg_04b
```

The leading `NN_` in a filename only fixes the order in the menu; it is stripped from the name shown
in game, so `03_Vegetation & Nature.txt` browses as **Vegetation & Nature**.

## Adding an object to a category

Append its model name to the category's `.txt` and restart the script. The name does **not** have to
exist in `ObjectList.ini` — a model the list has never seen is resolved by name and folded into the
database, so it will also turn up in search and save correctly.

To move an object, delete the line from one file and add it to another.

## Filtering a category by DLC

Every prop category opens with a **DLC** row at the top. Scrolling it left/right narrows the list below
to the objects one add-on pack shipped — the pack is read off the prefix on the model name
(`vw_prop_casino_art_01a` is the Diamond Casino, `h4_prop_...` is Cayo Perico), and anything no prefix
claims counts as **Base Game**. A category only offers the packs it actually contains, so the filter can
never empty the list. The table of prefixes lives in `Dlc.cs`.

Vehicles and peds have no such row: both lists are keyed by ScriptHookVDotNet enum names (`Deveste`,
`Hooker01SFY`), which carry no DLC prefix to read.

## Favorites

The first category of every type is **Favorites**, and it is the one the mod maintains itself. Highlight
any object, vehicle or ped — in a category or in the search results — and press the *Favorite* button
(`E` / gamepad `X`) to star it. Starred models are marked with a star wherever they are listed, and
pressing the button again unstars them.

The list is written straight back to disk, one file per type, so it survives a restart:

```
scripts\MapEditor\Favorites\Props.txt
scripts\MapEditor\Favorites\Vehicles.txt
scripts\MapEditor\Favorites\Peds.txt
```

The format is the same as a category file, so the list can be edited or shared by hand — but unlike a
category file, a model name the object list does not know is dropped rather than resolved, because this
file is written by the mod and an unknown name in it is a stale entry.

## Adding a category

Drop a new `.txt` into the folder. Number it to place it in the menu.

Nothing can fall through the cracks: any model that no file claims is collected into an
**Uncategorized** entry at the bottom of the menu, and **All** always lists everything.

## Regenerating

`Categories/Props/*.txt` was generated from `ObjectList.ini`. To re-derive the split from scratch
after the object list gains new models:

```
py tools/generate_categories.py
```

This **overwrites** the prop category files, discarding hand-edits — for a one-off change, edit the
`.txt` directly instead. Vehicle and ped files are rebuilt from the in-game settings menu
(*Rebuild Vehicle & Ped Categories*), which likewise discards edits to those two folders.
