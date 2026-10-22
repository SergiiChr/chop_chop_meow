# Animation asset provenance

Created with the built-in image-generation tool, using the committed player and worker atlases as character references. Final PNGs are committed and need no online service to play.

Shared prompt:

Use case: stylized-concept. PRODUCTION 2D GAME ANIMATION ATLAS matching the reference character exactly. Genuine TRANSPARENT background, no ground, shadows, text, lines or labels. EXACTLY FOUR equal columns and EIGHT equal rows =32 separate full-body sprites. Canvas1536x3072, every cell384x384. All eight rows MUST be present. Each cell holds one full figure at identical body size: head near y45, feet baseline near y345, centered hips x192; generous margins so tools NEVER cross cell borders. SAME body, clothing and lighting across ALL32 figures. Camera orthographic isometric30degrees. Smooth illustrated painterly game art, no pixels. Columns are directions: column1 facing southeast/front-right, column2 southwest/front-left, column3 northeast/back-right, column4 northwest/back-left. Within EACH column, rows1through8 are successive phases of ONE continuous action cycle, clearly changing limb positions every row, NOT repeated idle poses. Preserve adult proportions and consistent identity. No extra objects except held tools specified, no environment. Actions should be exaggerated enough to read when drawn85px tall.

Action specifications:

```json
[
  {
    "key": "player-walk",
    "ref": "player-atlas.png",
    "action": "Player in teal tunic and brown trousers/boots from reference, EMPTY HANDS, tool stowed. Eight phases WALK cycle down each column: left heel contact; left weight/down; right foot passing; right up; right heel contact; right weight/down; left foot passing; left up. Alternate legs and opposite arms naturally; hips remain almost fixed; foot motion and knee bend must change substantially. Row8 loops smoothly into row1."
  },
  {
    "key": "worker-walk",
    "ref": "worker-atlas.png",
    "action": "Worker in BLUE BIB OVERALLS and cream rolled-sleeve shirt, brown boots from reference, EMPTY HANDS. Eight phases WALK cycle down each column: left heel contact; left weight/down; right foot passing; right up; right heel contact; right weight/down; left foot passing; left up. Alternate legs and opposite arms naturally; hips remain almost fixed; foot motion and knee bend must change substantially. Row8 loops smoothly into row1."
  },
  {
    "key": "player-axe",
    "ref": "player-atlas.png",
    "action": "Player in teal tunic and brown trousers/boots. Hold one IRON AXE with dark grey broad wedge blade and wooden handle in BOTH hands. Eight row phases of CHOPPING A TREE: neutral low axe; begin pull-back; axe raised by shoulder; maximum backswing above/behind shoulder; rapid diagonal down swing; low forward wood impact with bent elbows/body; follow through low; recover to neutral. Feet stay planted. No tree or impact target in the image. Blade shape and handle length identical in all32 cells."
  },
  {
    "key": "player-pick",
    "ref": "player-atlas.png",
    "action": "Player in teal tunic and brown trousers/boots. Hold one IRON PICKAXE with a long narrow curved darkgrey two-ended pick head and wooden handle in BOTH hands, clearly NOT an axe. Eight row phases of MINING ORE: ready grip in front; lift pick to chest; lift overhead; maximum overhead backswing; downward arc; ground-level impact in front with bent knees and waist; low follow through; lift recovery to ready. Feet planted. No rocks/target in image. Distinct pick head identical all32cells."
  },
  {
    "key": "player-steel-axe",
    "ref": "player-atlas.png",
    "action": "Player in teal tunic and brown trousers/boots. Hold one refined STEEL AXE, pale BLUE-SILVER gleaming polished broad wedge blade, long dark wood handle, in BOTH hands. Dedicated confident two-handed FELLING chop: row1 neutral axe across body; row2 draw toward rear hip; row3 twist shoulders with axe high behind; row4 maximum shoulder-high lateral backswing; row5 strong horizontal forward swing; row6 low-forward impact with weight on leading leg; row7 recoil/follow through; row8 recover. This must differ from a mining motion and from a generic idle pose. Feet planted, no trees/target. Same steel axe shape every frame."
  },
  {
    "key": "player-steel-pick",
    "ref": "player-atlas.png",
    "action": "Player in teal tunic and brown trousers/boots. Hold one refined STEEL PICKAXE, pale BLUE-SILVER polished long narrow double-ended pick head, dark wood handle, in BOTH hands. Dedicated mining sequence eight rows: ready low; lift toward chest; lift past head; maximum overhead windup with knees straight; downward stroke; low-front rock impact with knees/waist bent; rebound; recovery. Clearly pick NOT axe. Steel blue-silver material readable. Feet planted, no rock/target. Same tool everyframe."
  },
  {
    "key": "worker-axe",
    "ref": "worker-atlas.png",
    "action": "Worker in BLUE BIB OVERALLS, cream rolled-sleeve shirt, brown boots. Two-handed IRON AXE tree chopping. Eight row phases: neutral low; begin pull-back; shoulder raise; maximum overhead/behind backswing; downward diagonal swing; low-forward wood impact; follow-through; recovery. Feet planted, clear changing arm and body poses, iron wedge axe identical all32cells. No tree/target."
  },
  {
    "key": "worker-pick",
    "ref": "worker-atlas.png",
    "action": "Worker in BLUE BIB OVERALLS, cream rolled-sleeve shirt, brown boots. Two-handed IRON PICKAXE mining. Long narrow two-ended curved grey pick head not an axe. Eight rows: low ready; chest lift; head lift; maximum overhead windup; downward swing; low-front rock impact knees bent; follow-through; recovery. Feet planted, clear changing arm/body poses. No rock/target."
  },
  {
    "key": "worker-pump",
    "ref": "worker-atlas.png",
    "action": "Worker in BLUE BIB OVERALLS, cream rolled-sleeve shirt, brown boots. Operating a furnace BELLOWS handle with BOTH EMPTY HANDS held out together in front, but do NOT draw bellows or machine. Eight rows of pumping: arms forward at waist; arms rise toward chest; high handle position shouldersup; lean bodyweight forward; press hands downward strongly; lowest full down-stroke kneesbent; ease back; return to neutral. Feet planted, arms and torso clearly change phase; no tool or machine in any cell."
  },
  {
    "key": "worker-carry",
    "ref": "worker-atlas.png",
    "action": "Worker in BLUE BIB OVERALLS, cream rolled-sleeve shirt, brown boots. Carry one small wooden transport CRATE with BOTH hands against waist/chest, elbows bent around crate. Eight phase WALK cycle left contact, down, passing, up, right contact, down, passing, up. Alternate knees/legs and keep crate steady against torso; no free arm swinging. Crate same size/positionrelativebody all32frames. Preserve silhouette and feet baseline. No extra cargo outside crate."
  },
  {
    "key": "worker-hammer",
    "ref": "worker-atlas.png",
    "action": "Worker in BLUE BIB OVERALLS and cream rolled-sleeve shirt, brown boots. BLACKSMITH HAMMERING with a SMALL short-handled iron HAMMER in right hand and a short ingot held by tongs in left. Eight rows in each direction: hammer low ready; raise toward shoulder; raise above head; high backswing; hammer descends; low impact in front at waist height; slight rebound; recovery. Feet planted, do not show an anvil or environment. Clearly a short block-headed hammer, NOT a long pick or axe. Left hand/tongs stay steady while rightarmmoves."
  },
  {
    "key": "player-pump",
    "ref": "player-atlas.png",
    "action": "Player in TEAL tunic and brown trousers and boots. Operating a furnace BELLOWS handle with BOTH EMPTY HANDS held out together in front, but do NOT draw bellows or machine. Eight rows of pumping: arms forward at waist; arms rise toward chest; high handle position shouldersup; lean bodyweight forward; press hands downward strongly; lowest full down-stroke kneesbent; ease back; return to neutral. Feet planted, arms and torso clearly change phase; no tool or machine in any cell."
  }
]
```

Walking revisions requested alternating left/right contact, passing and recovery poses across eight rows, with opposite arm swing. The final steel-pick sheet edits the eight-row iron-pick reference, changing only the equipment to polished pale silver-blue steel heads with a reinforced dark hardwood shaft while retaining the 32 poses and cell positions. Player hammer work uses a short block hammer and tongs holding an ingot, with shoulder lift, overhead windup, waist impact and recovery.

Each sheet has four facing columns (SE, SW, NE, NW) and eight time rows. The runtime isolates each connected actor, anchors the boots, and applies one shared body scale per clip so overhead tools do not shrink the person.
