# Art generators

Two programs that draw shipped image assets as geometry rather than exporting them from a design file.

```
dotnet run --file gen-icons.cs
dotnet run --file gen-control-images.cs
```

| | |
|---|---|
| `gen-icons.cs` | the three application icons and their installer badges — one drawing routine per mark, emitted as `.ico`, `.png`, `.icns`, the MonoGame window bitmap, and a contact sheet |
| `gen-control-images.cs` | the three control-scheme reference images the in-game Help panel shows |

Both write into the sibling `MirageCore` checkout. Neither touches a world folder: Mirage Core ships
no world of its own, and the content generators that write one belong to the game built on it.

⚠️ **Neither has a dry run.** They write their output the moment they start. That is safe — they only
ever overwrite their own generated assets, in known locations in the engine repository — but it is the
opposite of the content generators’ default in the other tools repository, so do not reach for
`--apply` here and assume nothing happened without it.
