# Loader's Heavy Slam
Makes the Loader's Thunderslam more satisfying.

## What does this mod do ?
It makes Thunderslam's **Radius**, **Damage** and **Knock-Up Force** scale with downwards speed. 

## How is this calulated ?
By default when activated you gain **-100 velocity** in the y axis *(i.e. Downwards)*. Then we take the absolute value of that speed and fed into the formula for calculation.

### The Formula
`newValue = value + (value * (speed / 100) * valueCoefficient)`
Where:
- *value* = the normal value that would be used *(e.g. 210 for damage)*
- *speed* = the velocity in the y axis *(100 for each second falling)*
- *valueCoefficient* = the coefficient as specified in the config *(e.g. 0.478 for damage by default)*