# Match-3 RPG — Economy & Progression

## Skill/mana system design
- Hero has base starting mana (15) + starting skill; curios ("диковинки") placed in inventory add flat mana that stacks with hero's own mana
- 3 skill slots per hero (not 2): differing mana cost/effect power. Costs: skill 1 = 15 mana, passive = 10 mana, skill 2 = 25 mana, skill 3 = 35 mana
- Passive skill can be toggled on/off; activating it reserves mana, reducing pool available for active skill — reservation mechanic similar to Path of Exile aura reservation
- Item mana bonus is added before reservation; skill selection can only be changed in hero menu, not mid-battle
- New active/passive skills for hero slots unlock as the hero levels up
- Equipment items are gated by hero level and rarity tier

## Campaign structure / progression pacing
- 18 levels per territory (8 territories total)
- After clearing a territory, farmable dungeons spawn there dropping gear/loot for progression
- When moving to a new territory, enemy strength is balanced so the player can't clear it immediately (requires farming/leveling first)
- Farming dungeon run length: 2-5 minutes per run
- Gating design: light/easy gating for territories up to Demons (5th race — one of the two most popular alongside Elves for collection appeal); entry gate into Demons territory stays light, but difficulty ramps up INSIDE the territory itself; the Demons race only unlocks after fully clearing (not just entering) that territory, to build anticipation

## Equipment rarity tiers tied to territory progression
- Green-rarity gear: from Dwarves and Elves territories
- Blue-rarity gear: unlocks at the Orcs territory
- Purple-rarity gear: unlocks in a new dungeon opening after unlocking the Beastfolk race, dropping once player enters Demons territory
- Design intent: player enters Demons territory with blue gear maxed out, then must farm the new purple dungeon + upgrade gear to beat the Demons territory boss
- Purple dungeon is open/accessible from entry (not gated behind a wall); a hint/prompt pointing to it shows if the player loses a level twice in a row
- Curio mana bonus by rarity/level: Green: lvl1=+1, lvl20=+10. Blue: min level 10 (+10 mana), lvl40=+20. Purple: min level 20 (+10 mana), lvl60=+35
- Once a purple curio is unlocked, it can be upgraded using any other item; non-purple curios can only be upgraded using the specific tier that will replace them
- Merge mechanic: feeding a lower-rarity item into a higher-rarity item transfers its level, but resulting mana follows the higher-rarity item's own scale at that level (e.g. feeding lvl40 blue into lvl20 purple → lvl40 purple giving 20 mana)
- Minimum item levels: Blue = 10, Purple = 20
- Chosen leveling approach: resource currency + duplicate-feeding + milestone-level special materials (Variant C)
- Demons-territory purple dungeon drops ONLY purple-rarity items (no lower tiers) — designed so gearing the whole squad requires relatively few runs
- Extra/duplicate purple items beyond what's needed are fed into already-equipped items as upgrade fodder

## Equipment slots
- 4 slots per hero: weapon, armor, earrings, curio
- Weapon is race-specific (only that race can equip it); armor/earrings/curio are universal across races
- Stat roles: Armor = HP, Earrings = Defense, Weapon = Damage, Curio = max mana

## Progress-currency (ОП) economy
- Sources: regular story levels, dailies, and dungeons in earlier (outleveled) territories (though farming old dungeons is not worth the time)
- 1 story level = 30 ОП; 1 full daily-quest set = 200 ОП baseline
- Target: finishing Elves+Dwarves (36 story levels) in one marathon day → green-item level ~15-18 (~1050-1550 ОП)
- "Fast Start" daily bonus curve (smooths transition for marathon players): Day 1 = 400 ОП (×2.0), Day 2 = 350 (×1.75), Day 3 = 300 (×1.5), Day 4 = 250 (×1.25), Day 5+ = 200 (base)

## Hero card rarity & ascension system (separate from item rarity)
- Rarities: Green / Blue / Purple / Orange
- Ascension count: Orange = 3, Purple = 2, Green/Blue = 0 (no ascension needed)
- Duplicate of an already-owned hero card → converts into a "gem" for that hero; gems used in hero menu to ascend the card (exact ascension effect not yet decided)
- If a duplicate drops after max ascension reached, it converts into a generic "hero card experience stone" instead (applies across all rarities)
- Level progression — ascension EXTENDS the cap rather than gating within a fixed range:
  - Green: 1→20 (no ascension)
  - Blue: 1→40 (no ascension)
  - Purple: base 1→60 → Ascension 1 → cap 80 → Ascension 2 (final) → cap 100
  - Orange: base 1→80 → Ascension 1 → cap 100 → Ascension 2 → cap 120 → Ascension 3 (final) → cap 160
