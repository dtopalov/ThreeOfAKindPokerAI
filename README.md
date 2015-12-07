# ThreeOfAKindPokerAI

### ThreeOfAKind Team Members:

| Име     | Academy nickname       | Github  |
| ------------- |:-------------:| -----:|
| Димитър Топалов  | topalkata | dtopalov  |
| Васил Динев    | vassildinev      |   vassildinev |
| Кирил Колев| kiko81     |    kiko81 |

#### Preflop logic
* getting starting hand strength
* getting position and implementing different logic when in the SB and when in the BB 
    * SB plays a bit more agressively in general in attempt to either steal the blinds or take advantage of the better postflop position
    * Both SB and BB apply different strategies based on hand type and blinds to stack ratio

#### Flop logic
* Getting current hand rank
    * if straight+ - Raise
* Counting outs (if we have pair - for set+ ; else for straight+)
    * if the hand contains no pair and outs are less than 6 - Fold
    * else raise or call depending on stack size, opponent actions and the number of outs
* different actions or bet sizes applied depending on the type of opponent - e.g. betting more vs calling stations with made hand

#### Turn logic
* similar to the flop logic, but with modified actions and bet sizes as there is only one more card to come

#### River logic
* Getting current hand rank
    * checking if our cards improve community cards significantly
* raising with strong and improved hand
* calling or betting with medium-strength hands
* folding with no hand and facing a raise
* bluffing when appropriate (opponent doesn't call too often or it is checked to us)

#### Common feats
* analyzing opponent and determining his playing style, based on a certain number of previously collected actions
* calling all raises less than 12
* applying different logic in some cases depending on the type of opponent

Conclusion: The code is very self documented

###### not our competitive version - something went very wrong here - released a previous version as final competitive `Competitive.cs`
