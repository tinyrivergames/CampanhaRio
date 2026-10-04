# Save model

Two kinds of save, matching the plan ("progress belongs to the host; each player keeps their own"):

| | Campaign save (the group's) | Player profile (each player's) |
|---|---|---|
| Who writes it | **The host only** (`CampaignState`) | Each player, on their own machine |
| What's in it | region, **checkpoint**, rivers (completed, best medal, best group time, attempts, failures), **van stickers**, **van trophies** | name, colour, **cosmetics** (unlocked, equipped per slot), **personal bests** per river |
| File | `<persistentDataPath>/Saves/campaign_<slot>.json` | `<persistentDataPath>/Saves/profile.json` |
| Code | `Scripts/Campaign/SaveData.cs` (`CampaignSave`, `RiverRecord`), `CampaignState.cs` | `SaveData.cs` (`PlayerProfile`, `EquippedCosmetic`, `PersonalBest`) |

`persistentDataPath` on Windows is `%USERPROFILE%/AppData/LocalLow/TinyRiverGames/CampanhaRio`. Test instances use
`-cc-savedir <dir>` so several instances on one PC never share files.

## Rules

- **JSON** (`JsonUtility`), human-readable, with a `version` field in each file. A future version reads the old file
  and fills in new fields with their defaults (JsonUtility leaves missing fields at their initializers). A breaking
  change bumps `version` and adds a small upgrade step in `SaveSystem.Load`.
- **Atomic writes:** each save is written to `<file>.tmp` and swapped in (`File.Replace`), so a crash mid-write never
  leaves half a file. An unreadable file is logged and replaced by a fresh one (never a crash).
- **The host writes when something changes:** a new checkpoint (the streamer: the furthest segment that holds the whole
  group), a river attempt (passed with time and medal, or failed), a sticker, a trophy. No autosave timer.
- **Clients never write the campaign.** A friend joining mid-campaign gets the host's world (scenes and objects through
  Netcode). Their own profile stays on their machine.
- **Rewards are cosmetic only** (the plan): nothing in either file makes anyone faster.
- **Checkpoints are segments.** The campaign restarts at the checkpoint segment's `entry`; failing a river never
  costs progress (you go back to the river start, not the road).

## Example (campaign)

```json
{
    "version": 1,
    "region": "Floresta",
    "checkpoint": "Test_C",
    "rivers": [
        { "riverId": "Rio_Teste_River01", "completed": true, "bestMedal": 3, "bestTime": 61.4, "attempts": 5, "failures": 4 }
    ],
    "vanStickers": [],
    "vanTrophies": [],
    "updatedUtc": "2026-10-03T21:40:12.1234567Z"
}
```
(`bestMedal`: 0 None, 1 Bronze, 2 Silver, 3 Gold.)

## Not yet (later phases)

- Several campaign slots in a menu (the code takes a slot number; there's only slot 0).
- The profile is defined but nothing writes it yet (no cosmetics exist). Personal bests come with the river results
  screen (Phase 7).
- Cloud saves (Steam) are out of scope.
