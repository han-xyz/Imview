# Imview

Imview is a desktop content editor for a Revive101 (Wizard101) server. It edits the world data the
Imlight server loads (quests, quest metadata, drop tables, creature decks, NPC inventories, NPC spell
inventories and zone transfers) and it reads the game client files: it views and unpacks KIWADs,
decodes object property blobs and builds quests from packet captures.

World data lives in a data pack: a folder of JSON files in the layout Imlight reads (your fork of
spiraldb). Imview reads and writes that folder directly; there is no database server.

## Prerequisites

- .NET 10 SDK (`global.json` pins `10.0.100`, later 10.0 feature bands are accepted).
- The submodules (Imcodec and WizardTea):

  ```sh
  git submodule update --init --recursive
  ```

- Imcodec's generator inputs. The build generates types from a type dump in
  `submodule/Imcodec/src/Imcodec.ObjectProperty/GeneratorInput/` and from the `*Messages.xml` files in
  `submodule/Imcodec/src/Imcodec.MessageLayer/GeneratorInput/`; see Imcodec's README for how to get them.
  They come from the game client: do not commit them.
- A local checkout of your spiraldb fork (the data pack), for example:

  ```sh
  git clone <your-spiraldb-fork-url> ~/spiraldb
  ```

- Network access to the patcher for client files. The default revisions URL is
  `https://patcher.r10.one/revisions`.

## Build, test and run

```sh
dotnet build Imview.sln -c Release
dotnet test tests/Imview.Tests/Imview.Tests.csproj -c Release
dotnet run --project src/Imview.Core -c Release
```

## First start

1. If no data pack folder is set, or the folder no longer exists, Imview opens the data pack window.
   Choose your spiraldb checkout and save. If you cancel, Imview still starts, but the editors that
   need world data refuse to open or report that no data pack is set until you choose a folder.
2. After each load Imview shows how many records it read per folder. It also lists the files it
   skipped: files that do not parse, and files whose key another file already holds (the first file
   in name order wins).
3. To pick the client revision, use **Configure Client Files** on the start page. Root.wad and the
   other client files are downloaded into the client file cache.

To change the data pack folder later, use **Data Pack...** on the start page.

## Settings and caches

Settings live in `config.ini` under the user's application data folder (`~/.config/Imview/` on
Linux, `%APPDATA%\Imview\` on Windows):

| Section and key | Meaning |
| --- | --- |
| `[DataPack] Path` | The data pack folder. |
| `[ClientFiles] RevisionsUrl` | The patcher's revision list. |
| `[ClientFiles] SelectedRevision` | The client revision to download. Only letters, digits, `.`, `_` and `-` are accepted. |

Downloaded client files are cached in `ClientFiles/` in the same folder.

## Data pack workflow

Imlight loads world data from the spiraldb repository named by its `SpiralDBRemoteUrl` setting. Point
that setting at your fork, then:

1. Edit and save in Imview. Each record is one JSON file in the data pack: an existing record is
   rewritten in place, and a new record is written as `<folder-lowercase>_<key>.json`.
2. Commit the changed files in your spiraldb checkout.
3. Push to your fork.
4. Restart Imlight. It fetches the fork when it boots, as long as `SpiralDBAutoFetch` is true, and
   it fetches only the branch named by `SpiralDBBranch` (default `main`), so push to that branch.

The folders Imview reads and writes are `QuestTemplates`, `QuestMetadatas`, `DropTables`,
`NpcInventory`, `NpcSpellInventory`, `CreatureSpellbook` and `ZoneTransfer`.
