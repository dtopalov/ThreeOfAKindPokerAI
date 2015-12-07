# ThreeOfAKindPokerAI

### ThreeOfAKind Team Members:

| Име     | Academy nickname       | Github  |
| ------------- |:-------------:| -----:|
| Димитър Топалов  | topalkata | dtopalov  |
| Васил Динев    | vassildinev      |   vassildinev |
| Кирил Колев| kiko81     |    kiko81 |

 * ### Preflop logic
     * getting starting hand strength
     * getting position and split logic if SB or BB 
        * SB plays a bit more agressively on first call
        * 
 * ### Flop logic
    * Getting current hand rank
        * if straight+ - Raise
    * Counting out cards (if have pair - for set+ ; else for straight+)
        * if no pair and outs less than 6 - Fold
        * else raise or call depending on stack and opponent actions
        * 
 * ### Turn logic
    * pretty much the same as flop logic except no matter if have pair
    * 
 * ### River logic
     * Getting current hand rank
        * checking if our cards improve commynity cards significantly
     * raising with strong and improved hand
     * folding if no hand and face opponent raise
     * 
 * ### Common feats
    * analyzing opponent actions and determining his style of play -  collecting actions and if percent of some of them hits some value - stops analysys and treat opponent as agressive or passive
    * calling all raises less than 12
    * 
Conclusion: The code is very self documented at all
###### not our competitive version - something went very wrong here - released a previous version as final competitive `Competitive.cs`