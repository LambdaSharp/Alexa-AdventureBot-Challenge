# June17-AdventureBot
AdventureBot is an Amazon Alexa Skill for creating your own voice-based adventures.

The AdventureBot uses a simple JSON file to define places with available player choices. The starting place is always called `start`.

## Commands
* 1 through 9: are player choices
* yes/no
* help
* hint
* restart
* quit

## Sample File
The following is sample adventure file:
```
{
    "places": {
        "start": {
            "description": "You are in a dark room. The air feels stale. You see a flicker of light to the North.",
            "choices": {
                "1": [
                    { "say": "You cautiously walk towards the North." },
                    { "goto": "room2" }
                ],
                "2": [
                    { "goto": "end-room-bad" }
                ],
                "hint": [
                    { "say": "You get the feeling that if you stay here, you will not be for long..." }
                ]
            }
        },
        "room2": {
            "description": "You are in dimly lit room. The air feels a bit fresher. There is a breeze coming from the East.",
            "choices": {
                "1": [
                    { "goto": "start" }
                ],
                "2": [
                    { "goto": "end-room-good" }
                ],
                "hint":[
                    { "say": "You feel like you're out of immediate danger now." }
                ]
            }
        },
        "end-room-good": {
            "description": "You found the exit! Congratulations!",
            "choices": {
                "restart": [
                    { "say": "You feel like the world is beginning to spin around you." },
                    { "delay": "1.0" },
                    { "say": "And then it's gone!" }
                ]
            }
        },
        "end-room-bad": {
            "description": "Before you know it, the air turns toxic and you suffocate. You've died.",
            "choices": {
                "restart": [
                    { "say": "It feels like a hand is reaching out from the light and pulls you back." },
                    { "delay": "1.0" },
                    { "say": "And then you are back!" }
                ]
            }
        }
    }
}
```