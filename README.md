# Rain Fills Containers
A Vintage Story Mod by UnanimousVeto

## Purpose
This mod causes exposed containers (including those stored on top shelves
and ground storage) to fill with water when it is raining.

## Configuration
### rainCheckDeltaMS
* integer > 0
* defaults to 5000
* requires reload

### minimumPrecipitation
* float >= 0
* defaults to 0.04 (the level at which temperature sensitive blocks are considered exposed to rain)
* modifiable by command

### fillRate
* float >= 0
* defaults to 1
* modifiable by command

### smallStorageFillRateMultiplier
* float >= 0
* defaults to 0.25
* modifiable by command

### snowFillRateMultiplier
* float >= 0
* defaults to 0.5
* modifiable by command

### snowRequiresWater
* true or false
* defaults to true
* modifiable by command

### blockBlacklist
* list of block code strings
* default contains "verticalboiler-*", because the model is closed (though currently the height of this block will cause it to occlude itself from rain)
* requires reload

### itemBlacklist
* list of item code strings
* default contains "jug-*", because the mouth of the model is quite small
* requires reload

## Commands
### /rainfillscontainers
Displays the current mod configuration.  

### /rainfillscontainers minprecip *precipitation-level*
Overrides the configured precipitation level.
A negative argument will restore the original value.

### /rainfillscontainers fillrate *fill-rate*
Overrides the configured fill rate.
A negative argument will restore the original value.

### /rainfillscontainers smallfillrate *small-fill-rate-multiplier*
Overrides the configured fill rate multiplier for smaller containers,
(i.e. ground stored items or items on shelves).
A negative argument will restore the original value.

### /rainfillscontainers snowfillrate *snow-fill-rate-multiplier*
Overrides the configured fill rate multiplier applied while it is snowing.
A negative argument will restore the original value.

### /rainfillscontainers snowrequireswater *true-or-false*
Overrides the configured boolean that determines whether
water must already be in a container before snow will melt
and fill it with water like rain does.

### /rainfillscontainers restoresettings
Restores all overridden settings to match the values originally loaded
from the config file.

### /rainfillscontainers updateconfig
Updates the config file with the current overrides.
This command is irreversible, but the default settings can be restored
by deleting the config file and relaunching the game.
