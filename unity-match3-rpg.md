# Unity Match-3 RPG Project

## Project overview
- Unity 6 project: 3D match-3 puzzle game combined with turn-based RPG combat
- Board is a 7x7 (Width/Height configurable) grid of 3D gem models (diamond shapes: Round, Cushion, Trillion, Oval, Pear, Marguise, Asscher, Radiant, Princess, Baguette, Emerald, Heart), 6 colors: Red, Blue, Green, Yellow, Violet, Pink
- Camera looks straight down (Rotation X=90), Orthographic or Perspective depending on latest tweak, positioned above grid center
- Two scenes: MainMenuScene (hero collection + squad selection) and SampleScene (battle scene with grid + combat UI)

## Core scripts and architecture

**GridManager.cs** — manages the board
- width/height/cellSize fields, grid[,] array of Item
- GenerateBoard/SpawnItem/GetWorldPosition/SwapItems/FindMatches/ProcessMatches/CollapseGrid
- SwapItems fixed bug: must store both items' original coords separately (aX,aY,bX,bY) to avoid overlap on failed swap revert
- SpawnItem uses `prefab.transform.rotation` (not Quaternion.identity) so gem model rotation set on prefab is respected
- HasPossibleMoves/WouldCreateMatch/CheckMatchAt/ReshuffleBoard — deadlock detection and board reshuffle when no moves possible; called in Start() and after CollapseGrid resolves with no matches
- ExecuteConvertAndDestroySkill(int convertCount) — converts N random non-red gems to red, destroys all red gems on board (uses redTypeIndex field, must match Red's index in itemPrefabs array)
- ExecuteDestroyRowsSkill(rowStart, rowEnd) — destroys all gems in a row range
- Item Prefabs array order in this project: Element 0=RedGem, 1=PinkGem/BlueGem (varies by scene — MUST verify actual order each time, was source of bugs), redTypeIndex must match actual position
- OnDrawGizmos added to visualize grid bounds (yellow wireframe) in Scene view

**Item.cs** — single gem behavior
- x,y,type fields; OnMouseDown handles selection (pulse animation via SetSelected/PulseRoutine) and swap-with-neighbor logic via static `firstSelected`
- MoveTo/MoveRoutine for slide animation between grid positions
- PlayDestroyAnimation() — shrink+rotate animation, also instantiates destroyEffectPrefab (particle system), colors particles via renderer.material.color with fallback to `_BaseColor` (glTF/shader-graph materials don't have `_Color` property — must check HasProperty first)
- Gem 3D models imported via glTFast from a .glb file (diamond_gem_shape_set), materials duplicated per color (Diamond_Red, Diamond_Blue etc.) from Diamond_Master, each assigned to Element 0 of Mesh Renderer on each gem prefab
- Each gem prefab has MeshCollider (Convex=true, Mesh assigned matching the Mesh Filter) — collider Mesh field must not be None or clicks fail silently

**BattleManager.cs** — combat logic
- playerHP/enemyHP/playerShield, enemyMinAttack/MaxAttack
- Resource system evolved: originally global `resources[6]` array (one pool per color) → now per-hero: `heroRoster` (HeroData[]) + `activeHeroes` (List<HeroRuntimeState>), each hero has own currentResource independent of others of same color
- Damage color logic: Red/Blue/Green/Yellow/Violet (indices 0-4) all deal damage AND give mana of their own color when matched; Pink (index 5) heals player AND gives mana to ALL FIVE damage colors at once; Shield only comes from skills now, never from normal matches
- damagePerGem[5] array (per color), pinkHealPerGem
- damageMultiplier + damageMultiplierTurnsRemaining for temporary buffs (decrements each player turn in ResolvePlayerTurn)
- TryUseSkill(HeroRuntimeState hero, SkillData skill) — checks hero's own currentResource, applies effect by SkillEffectType
- ResolvePlayerTurn(Dictionary<int,int> matchedTypeCounts) called from GridManager after cascades fully resolve; loops all activeHeroes matching resourceType to add resource to EACH hero of that color independently
- Awake() now pulls heroRoster from `HeroCollectionManager.Instance.squad.Where(h => h != null).ToArray()` instead of manual Inspector list — squad is populated from main menu

**SkillData.cs** (ScriptableObject, Create → Battle → Skill)
- SkillEffectType enum: Damage, Heal, Shield, ConvertAndDestroyRed, DestroyRows, ShieldPercent, DamageBuffTurns
- Fields: costType (ResourceType), cost, effectType, effectValue, rowStart/rowEnd (for DestroyRows), shieldPercentOfMaxHP, damageMultiplier + buffDurationTurns (for DamageBuffTurns)
- ResourceType enum: Red=0,Blue=1,Green=2,Yellow=3,Violet=4,Pink=5

**HeroData.cs** (ScriptableObject, Create → Battle → Hero)
- heroId (string, stable identifier independent of asset filename — auto-fills via OnValidate() from asset name if empty, must NOT be manually changed after release or save data breaks)
- heroName, resourceType, portrait (Sprite), themeColor, maxResource, skills[] (array, was single skill before — now supports multiple)

**HeroRuntimeState** (plain class, not MonoBehaviour) — wraps HeroData + currentResource for a specific battle instance

**HeroCardUI.cs** — battle UI card per hero
- References heroData, battleManager, portraitImage, fillImage, activateButton, buttonOverlay
- ApplyHeroData() reads heroData.portrait/themeColor and looks up heroState via battleManager.GetHeroState(heroData)
- Fill bar (Image Type=Filled) shows currentResource/maxResource; activateButton pulses via sine wave alpha overlay (min/maxAlpha, pulseSpeed), dimmer when resource insufficient
- Now spawned dynamically at battle start (not manually placed per-color) via BattleHeroCardsSpawner.cs which reads battleManager.activeHeroes and instantiates HeroBattleCard prefab into HeroCardsPanel (Horizontal Layout Group container)

**BattleUI.cs** — top HP bars / resource display
- playerHPSlider/Text, enemyHPSlider/Text, playerShieldText
- HeroResourceUIEntry class (heroData + amountText) replaced old fixed 6-color resource display, now list-based per hero via battleManager.GetHeroState

## Hero Collection / Squad menu system (MainMenuScene)

**HeroCollectionManager.cs** — singleton (DontDestroyOnLoad), central data store
- allHeroes (HeroData[]), ownership (List<HeroOwnershipData>: heroId+isUnlocked+level), squad (List<HeroData>, always exactly 4 slots, null = empty)
- slotBeingEdited (int, -1 = not editing) — set via StartEditingSlot(index) when player clicks a squad slot
- AssignToSlot(HeroData) — called when clicking a hero card in collection while slotBeingEdited >= 0; removes hero from any other slot first (no duplicates in squad), saves via PlayerPrefs (squad_ids as comma-joined heroId string, empty string for null slots)
- TEMP: Awake() force-unlocks all heroes for testing (`foreach UnlockHero(hero)`) — remove when real unlock progression exists

**HeroCollectionUI.cs / HeroCollectionCardUI.cs** — collection grid screen
- Populates gridContainer (Content under ScrollView, has Grid Layout Group Cell Size 150x200 + Content Size Fitter Vertical=Preferred Size) with heroCardUIPrefab instances
- Card shows portrait, lockOverlay (if not unlocked), selectButton; clicking calls collectionManager.AssignToSlot if slotBeingEdited active, else just logs a hint message

**SquadUI.cs / SquadSlotUI.cs** — squad screen, 4 fixed slots (NOT scrollable, uses Horizontal Layout Group not ScrollRect)
- SquadSlotUI has slotIndex (0-3, auto-assigned in SquadUI.Awake()), separate selectButton (click anywhere on slot → opens collection to pick hero) and removeButton (small button to clear slot)
- Must call Initialize(squadUI) on ALL slots (even empty ones) in Awake() so parentUI is never null when clicked — was source of NullReferenceException bug
- mainMenuUI reference needed on SquadUI to call ShowCollection() when a slot is clicked

**MainMenuUI.cs** — panel switcher
- collectionPanel/squadPanel GameObjects, toggles active via ShowCollection()/ShowSquad(); ShowSquad() also calls squadUI.RefreshSlots()
- StartBattle() → SceneManager.LoadScene(battleSceneName), battleSceneName="SampleScene"

## Recurring bugs encountered (watch for these patterns)
- Duplicate MonoBehaviour components on same GameObject (e.g. two Item scripts, two HeroCardUI scripts) — happened multiple times from copy-paste workflows, causes GetComponent<T>() to grab wrong instance or double event firing. Always check Inspector for duplicate components after copying objects.
- Prefab instances block structural changes ("Cannot restructure Prefab instance" dialog) — solution used: right-click → Prefab → Unpack Prefab to freely edit hierarchy in scene
- UI Rect Transform positioning bugs were extremely common: wrong Anchor Presets (stretch vs fixed), leftover Pos X/Y from before resizing a parent, Canvas showing as giant plane in Scene view (normal editor behavior, not a bug — check Game view instead)
- TextMeshPro: project uses TMP_Text; must import TMP Essential Resources via Window→TextMeshPro if missing; CS0246 TMP_Text errors usually mean package/using directive missing, not missing resources
- Unity's built-in Particles materials (e.g. ParticlesUnlit) are read-only — must duplicate into own Material asset to edit Base Map/texture
- glTF-imported materials use shader `glTF-pbrMetallicRoughness-Clearcoat` which lacks `_Color` property — code reading material color must check HasProperty("_Color") then fallback to "_BaseColor"
- Input System: project set to "Both" (old Input Manager + new Input System Package) in Player Settings — required restarting Unity Editor after changing this setting for OnMouseDown to work
- Grid Layout Group Cell Size accidentally set to huge values (e.g. 700x400) when trying to "see" portraits better — actual fix was correcting the HeroCollectionCard prefab's Portrait Image Anchor/size (Stretch-Stretch, no "Set Native Size") rather than inflating cell size

## User preferences/context
- User communicates in Russian in chat, code comments/UI text in Ukrainian
- User is learning Unity through this project, needs step-by-step screenshot-based troubleshooting guidance — very iterative debugging style expected
- User wants to eventually style the UI (dark fantasy background, custom fonts via TMP Font Asset generation, 9-sliced frame sprites for cards) — this was started but not finished

## Current state / in-progress at end of last session
- Just fixed: HeroCardsPanel (battle scene) had wrong Rect Transform (Pos Y=-1080, Width/Height=100x100, Scale 0.75) causing dynamically-spawned hero cards to be positioned off-screen — was mid-fix (recommended Bottom-Center anchor, Pos Y=100, Width=900 Height=200, Scale reset to 1)
- BattleHeroCardsSpawner.cs created and working (cards do spawn as children, just mispositioned)
- Squad menu selection flow (click slot → pick hero → assigns to that slot) is working correctly
- Collection screen scrolls correctly to show all 5 heroes (Hero_Blue/Green/Laart/Red/Yellow existed at last check)

## Next steps (not yet done)
- Finish fixing HeroCardsPanel position/size in battle scene so spawned cards are visible
- UI styling: background, custom TMP fonts, 9-sliced card frames (discussed but not implemented)
- Real hero unlock progression (currently all heroes force-unlocked for testing)

## Game concept / lore / design
- Genre: mobile F2P match-3 + RPG + strategy hybrid, dark fantasy anime art style
- Lore: undead army has conquered almost the whole continent; last human city (player's) still holds out; other races are enslaved by the undead, who hunt specifically for mages (only 2% of population can use magic) to use against a mage fortress blocking the path to another continent
- Main character is cursed while retaking the first city, becomes commander/strategist (does not fight personally), uses own magic to hold back the curse
- All heroes of every race are mages; races unlock progressively as story advances; each race has a unique skill tied to one specific match-3 gem color
- Race unlock order: Elves → Dwarves → Orcs → Beastfolk → Dragonkin → Demons → Angels → Humans (last, rarest race — humans were hunted hardest by the Undead King, who was human himself); base/home city is a refugee camp of escaped humans
- Elves and Demons chosen as the two races with the best/most anime-appealing visual style
- Squad has a "weight/cost" system: each hero has a party cost, limited by a capacity stat tied to the cursed commander (raised by upgrading base buildings)
- Base building: energy/resource generation + squad capacity upgrades; map has node-based dungeons for resource farming and territory capture
- Damage system: living hero of a color deals 8 dmg per match; if no hero of that color is in squad, or that hero has died, matches of that color deal 5 dmg instead (color also visually dims on hero death, and that race's ability becomes unavailable)
- Boss fights can have color restrictions: bringing a hero whose color the boss "absorbs" empowers the boss (e.g. +50% boss attack per matching hero)
- Permadeath dungeon ("death dungeon"): always open, 2-week cooldown per clear, no equipment bonuses apply (stat equalizer), boss drops let permanently change a hero's card appearance (skins); losing = squad heroes are permanently lost
- Escape mechanic in the death dungeon: main squad can flee (survives), but must sacrifice substitute heroes of the same level who die instead; fleeing squad gets a 1-week debuff and 1-week dungeon lockout; sacrificed heroes later reappear as undead bosses attacking the player's resource mines
- Skin system: non-playable "armor fragment" cards drop from content; collecting a required count and "merging" them permanently changes a hero's card artwork (skin), crafted via a forge/menu
- Temporary hero rental ("test-drive"): players can get time-limited access (e.g. 24-48h) to heroes of not-yet-unlocked races, via ads, paid rental, or free story-triggered guest appearances
- PvP: async duels wagering rare skin cards (from the death dungeon) as stakes, winner takes loser's staked skin card, limited to 3 duels per week; player's squad+AI fights the challenger (no real-time sync needed)
- Per-chapter unique field debuffs tied to proximity to the undead capital (increasing difficulty toward endgame): frozen/locked tiles, poison tiles with a turn counter before they trigger damage, cursed cells that block a color's racial skill, mana-draining tiles, etc.
- "Gambling board" mechanic: once per battle, player can flip the main match-3 board to reveal a hidden 3x3 grid (empty center, 8 blurred tiles: 4 positive effects, 4 negative), shuffle, then pick one tile at random for an immediate positive or negative battle effect, then board flips back — usable at any point in battle, only once
- Final boss (Undead King, lore-wise a corrupted human) forcibly triggers the gambling board mechanic against the player during the fight — all 8 tiles are negative, but severity varies (mild vs. severe negative effects)
- Elite/legendary heroes are recruited via a "summon" system — teleportation from the Mage Citadel (a besieged stronghold holding the strongest mages of all races), using a premium currency ("Space Crystals"); regular/common heroes are found via story progression instead
- Summon rarity tiers: Common/Rare/Epic/Legendary, plus a very rare (~0.05%) chance on a x10 summon to guarantee two Legendary heroes of the same race at once

## Skill design table (race × rarity) — finalized
- Elves: Common=destroy N random gems, Rare=destroy a harmful/debuff tile on the battlefield, Epic=turn a cell into a joker, Legendary=full board reshuffle in player's favor (guaranteed big match)
- Dwarves: Common=fixed shield, Rare=reduce enemy armor for N turns, Epic=invulnerability for 1 turn, Legendary=shield + % damage reflection combined
- Orcs: Common=fixed damage, Rare=multi-hit (2-3 hits), Epic=damage % of enemy current HP, Legendary=sacrifice % own HP → x3 damage to enemy
- Beastfolk: Common=extra turn, Rare=stun enemy 1 turn, Epic=damage scaling with matches made this turn, Legendary=two free swaps in a row
- Dragonkin: Common=fixed AoE damage, Rare=delayed damage mark (explodes after 2 turns), Epic=damage % of enemy max HP, Legendary=remove all own debuffs + AoE damage at once
- Demons: Common=reduce enemy accuracy, Rare=steal a buff from enemy, Epic=weakness mark (next hit on enemy empowered), Legendary=provoke + transfer own debuff to enemy
- Angels: Common=fixed heal, Rare=fully refill one hero's mana, Epic=team-wide debuff immunity for N turns, Legendary=resurrect a dead hero
- Humans: Common=transfer mana between heroes, Rare=reduce cost of an ally's next skill, Epic=copy ally's last used skill for free, Legendary=temporarily borrow another race's legendary skill

## Enemy design notes
- Undead enemies (main game faction) are immune to poison/DoT effects — green hero skills need a non-poison mechanic against them (design discussed: armor reduction, resource drain, buff removal, etc. instead of poison)
