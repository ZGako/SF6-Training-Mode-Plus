# SF6 Training Mode Plus

Collection of training mode scripts that aim to improve your ability to use lab inside SF6's training mode.

# Installation

Both [REframework](https://github.com/praydog/REFramework) and [\_ScriptCore](https://www.nexusmods.com/streetfighter6/mods/3160) are required to run this mod.
Suggested installation is to install this mod through the [fluffy mod manager](https://www.nexusmods.com/site/mods/818).

# Features

The mod features separate and independent "modules", and all of them aim to help with labbing.

## Training Mode Settings and Randomizer

This module is the main feature of the mod, it allows you to modify the training mode parameters with higher granularity, enables access to parameters not accessible by default, and lastly allows you to randomize them.

In particular, the following parameters are modifiable:

- Health: 1% increments (same as the base game)
- Drive:
  - stock increments (stock = bar of drive)
  - 0.1 stock increments
  - integer units (1 stock = 10000 units)
- Super:
  - stock increments (stock = bar of super)
  - 0.1 stock increments
  - integer units (1 stock = 10000 units)
- Character specific gauges:
  - For timed installs, you can set the install timer to start at any specific place rather than at the maximum
  - For resources, the functionality is the same that is provided by the game
- Positioning
  - Relative player distance, you can set a fixed player distance that is applied regardless of the screen position where you reset (center/left/right).
  - Absolute pivot point distance, you can move the pivot point from which the "relative player distance" gets calculated, this means being able to start from anywhere on screen.
    - To further elaborate, there is a lot of customizability on from where the calculation starts, you can set the pivot point to be in either absolute screen coordinates, or units from either corner.
    - Furthermore, the "relative player distance" pivot can be either the point inbetween both characters, or fixed to either of the characters.
  - Both of the above mentioned settings also have preset values to allow for easy selection without having to mess with the units (e.g Point Blank,Close Range,Throw Tech distance,Roundstart Distance, etc.).

For each of the above settings, a randomizer is also available. The randomizer supports both bounded and unbounded modes:

- In unbounded mode, the random value will be within the domain of the parameter's values. Be warned that when a discrete option is selected (such as stocks for drive/super), the randomized value will have an entire "stock" as a unit. Change your modality to 0.1 increments or units to have better granularity.
- In the bounded mode, you can set an upper and lower bound (also available for the discrete values when those are available), which limit the randomized value.

The randomizer will not go into effect on training mode refresh. You have a button within the REframework UI to both refresh and randomize.

Alternatively, a rebindable key is available, which works even with the REframework UI hidden.
My personal suggestion is to bind this to something like FUNC + [your current refresh bind], to keep your reset muscle memory and treat it more like a modifier (this work since the FUNC bind prevents the refresh bind to work).

## Character Info Display

This is a simple module that displays live data of the characters. In particular, the following information is displayed:

- Current health out of max health (e.g 1520/10000). When gray health is detected, both the current effective HP and the recoverable HP values will be displayed.
- Drive gauge in units
- Super gauge in units
- For timer installs, the current value left out of the maximum (e.g. 146/700).
- The current position of both characters, and their relative distance.

This module is intended to supplement the previous one by allowing you to check the particular values you may want (in particular positioning), before setting them either as parameters or randomizer bounds.

## Game Speed Plus

This module allows for more game speed options beyond the game's default 100% and 50%.
The range of supported speeds goes from 150% to 50% in 10% increments.

A refresh may sometimes be required due to training mode quirks.

# Suggested usages

## Conversion and "lethal" training

Setup a standard "neutral" drill, possible recordings are as follows:

- Character moving left 8F (probability - 5)
- Character moving right 8F (probability - 5)
- Jump In (probability - 1/2)
- 2MK/2MP (probability - 2/3)
- Drive rush button (probability - 2/3)
- DI (probability - 1)
- Character specific gimmick? (Cammy hooligan, kimberly OD TP, etc.)
- Fireball
- ETC.

The settings above are a general guideline, the idea is to have enough options to push the limits of your mental stack.

NOW, set up the randomizer to randomize any of the settings you want to work on. Generally I'd suggest to limit the HP ranges to lower values, especially if you want to practice identifying kill ranges.

Randomize your training mode and go through the drill, when you get a hit (be it a whiff punish, a DR check, an AA, etc.), try to be as optimal as possible (kill, get best oki, conserve resources, etc.). If you're not confident in your "solution", you can refresh without randomizing to try and optimze better.

## Health, DI and Super awareness

Although these are things you can practice together with the previous drill, initially its better to not overload your mental stack fully.

You may have experienced some of the following situations ingame:

- Counter DIng without enough HP
- Counter DIng at < 1 drive bar
- DIng a move like Luke Heavy Charged Knuckle while he had SA3, leading to you eating a fat punish
- Pressing BUTTON DRC into a DI, only for the DRC to burn you out and you not being able to counter DI
- The opponent does BUTTON DI, with the button burning you out, and you fail to counter the DI, leading to a stun.

Many of these situations have clear counters (PP chiefly), but they require you being "aware" of the situation.
Some of these, especially when it comes to specific character interactions, are difficult to train using just raw match experience (moreso against uncommon characters).

Set the randomizer to a small range around the threshold values to build awareness. Start from one thing (e.g. counter DIng at low vs high hp), and then add more options to increase your mental stack (low/high HP + low/high drive).

Finally, you can introduce this training into the above drill.

## Anything really

The examples I set are fairly simple, but don't stop them from exploring possibilities.
Introducing randomness when labbing and drilling both your own characters options and counterplay to your opponent's can greatly increase your "robustness" in real matches.

Have fun labbing!

# Long term features that I may or may not implement

This is a list of features that I may or may not be feasible (I'll identify them as such), that I could potentially implement (contributions are also accepted):

- [Feasible] Randomize health/drive/super/unique gauges upon loading a save state.
- [Maybe feasible] Store savestates across sessions per character.
- [Feasible but tedious] Add support for training "presets" where you set various things and stuff.
- [Feasible] Exporting and Importing character recordings - Sets of recordings also possible
  - Exporting menu allows for recording selection for set export - Allow for naming of files
  - Importing menu allows to also only importing single recordings of sets (they'll be separate files anyways)
- [Most likely feasible] Export and Import Save States (as they are ingame)
- [Feasible] Export "save state" from recording (might even be better to make it a fake savestate so you can use it as a basis for a randomizer drill)
