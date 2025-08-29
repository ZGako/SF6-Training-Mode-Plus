# SF6-Training-Mode-Plus
REframework mod for SF6 to expand the training mode functionality

## Features and notes
#### Short term goals
- [x] Game Speed - Allow control ranging from 50% to 150% at 10% increments.
- [ ] Alter Training Mode refresh parameters (these persist through refreshes, also gauge settings are compatible with fixed and refill unless stated otherwise)
    - [ ] Advanced Health - Allow unit control (compared to default training mode allowing only % increments, equivalent to 100 units). NOT COMPATIBLE WITH REFILL AND FIXED gauge setting
    - [ ] Drive - Allow control at 0.1 drive bar increments
    - [ ] Advanced Drive - Allow unit control (Not default since one bar is 10000 units)
    - [ ] Super - Allow control at increments of 1/100th of a bar
    - [ ] Advanced Super - Allow unit control (Not default since one bar is 10000 units)
    - [ ] Relative Player Distance - Allow controlling the player distance upon reset, still mantaining the LEFT - CENTER - RIGHT controls built into the game. Options will be:
        - POINT BLANK
        - LIGHTS RANGE (around max throw range) 
        - MEDIUMS RANGE (around 2MK range) 
        - HEAVIES RANGE (around 5HK range) 
        - FAR RANGE (jump in range)
        - MAX
    - [ ] Advanced Relative Player Distance - Allow unit control of player distance (setup allows for visual tuning before "locking in" the value)
    - [ ] Player Position - Allow for preset positions (dividing half the screen in 3 parts), and then utilizing the relative player position in the following 2 ways:
        - [ ] Set one of the Players as the fulcrum and offset the other by the relative player distance
        - [ ] Set the midpoint of the characters as the fulcrum
    - [ ] Advanced Player Position - Allow unit position control, two modes (with visual tuning beforehand):
        - [ ] Set both characters positions
        - [ ] Set midpoint between characters, then use relative player distance
        - [ ] Set one character's position, and then based on relative player distance position the other
    - [ ] Advanced Unique Character Gauges - For characters with install level 2s, start with a specific amount of gauge left (CONFIRM FEASABILITY FIRST)
- [ ] Randomizer for training mode parameters - These persist between resets - Allow for randomization options of the following parameters:
    - [ ] Health - Allow choice of interval (to have more guaranteed variety) - Allow choice of Upper and Lower bound.
    - [ ] Drive - Choice of interval (Including entire stock) or smaller - Allow for upper and lower bound.
    - [ ] Super - Choice of interval (Including entire stock) or smaller - Allow for upper and lower bound.
    - [ ] Relative Player Distance - Allow between discrete choices presented above or randomizing the unit value - Allow upper and lower bounds - Allow intervals for unit change.
    - [ ] Player Position - Upper and Lower bounds - Choice between midpoint fulcrum or player fulcrum (player closest to edge probably) regarding player distance - Allow interval - Have presets with bounds and fulcrum so that one can choose LEFT - CENTER - RIGHT and have it work (or have the discrete 1/6th values instead)
    - [ ] Unique Character Settings - Allow bounds were it makes sense - Enable starting gauge randomization if it makes sense - Stock randomization
- [ ] Exporting and Importing character recordings - Sets of recordings also possible
    - [ ] Exporting menu allows for recording selection for set export - Allow for naming of files
    - [ ] Importing menu allows to also only importing single recordings of sets (they'll be separate files anyways)
#### Maybe if doable
- [ ] Implement history for REframework UI
- [ ] Export and Import Save States (as they are ingame) - If not possible, implement fake save states
- [ ] Export save state from recording (might even be better to make it a fake savestate so you can use it as a basis for a randomizer drill)
- [ ] Mod the ingame UI and implement everything/partially there

## Project Structure

TODO
