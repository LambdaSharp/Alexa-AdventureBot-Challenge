# June17-AdventureBot
AdventureBot is an Amazon Alexa Skill for creating your own voice-based adventures.

The AdventureBot uses a simple JSON file to define places with available player choices. The starting place is always called `start`.

## Setup

### 1) Create IAM role for LambdaSharp-AdventureBotAlexa
You will need an IAM role to give permission to the lambda function to access CloudWatchLogs, S3, and SNS. You can create the `LambdaSharp-AdventureBotAlexa` role via the [AWS Console](https://console.aws.amazon.com/iam/home).

If you have the [AWS CLI](https://aws.amazon.com/cli/) tool installed, you can also create the role using the following statements run from this folder.
```
aws iam create-role --role-name LambdaSharp-AdventureBotAlexa --assume-role-policy-document file://lambda-role-policy.json
aws iam attach-role-policy --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/AmazonS3FullAccess
aws iam attach-role-policy --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/CloudWatchLogsFullAccess
aws iam attach-role-policy --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/AmazonSNSFullAccess
aws iam attach-role-policy --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/AmazonDynamoDBFullAccess
```

### 2) Create Alexa Skill (using Alexa Skill Builer)
```
{
  "intents": [
    {
      "name": "AMAZON.CancelIntent",
      "samples": []
    },
    {
      "name": "AMAZON.HelpIntent",
      "samples": []
    },
    {
      "name": "AMAZON.StopIntent",
      "samples": [
        "quit",
        "leave",
        "exit"
      ]
    },
    {
      "name": "Describe",
      "samples": [
        "describe"
      ],
      "slots": []
    },
    {
      "name": "Hint",
      "samples": [
        "hint"
      ],
      "slots": []
    },
    {
      "name": "No",
      "samples": [
        "no"
      ],
      "slots": []
    },
    {
      "name": "OptionEight",
      "samples": [
        "eight",
        "option eight"
      ],
      "slots": []
    },
    {
      "name": "OptionFive",
      "samples": [
        "five",
        "option five"
      ],
      "slots": []
    },
    {
      "name": "OptionFour",
      "samples": [
        "four",
        "option four"
      ],
      "slots": []
    },
    {
      "name": "OptionNine",
      "samples": [
        "nine",
        "option nine"
      ],
      "slots": []
    },
    {
      "name": "OptionOne",
      "samples": [
        "one",
        "option one"
      ],
      "slots": []
    },
    {
      "name": "OptionSeven",
      "samples": [
        "seven",
        "option seven"
      ],
      "slots": []
    },
    {
      "name": "OptionSix",
      "samples": [
        "six",
        "option six"
      ],
      "slots": []
    },
    {
      "name": "OptionThree",
      "samples": [
        "three",
        "option three"
      ],
      "slots": []
    },
    {
      "name": "OptionTwo",
      "samples": [
        "two",
        "option two"
      ],
      "slots": []
    },
    {
      "name": "Restart",
      "samples": [
        "restart",
        "start over"
      ],
      "slots": []
    },
    {
      "name": "Yes",
      "samples": [
        "yes"
      ],
      "slots": []
    }
  ],
  "prompts": [
    {
      "id": "Confirm.Intent-Restart",
      "promptVersion": "1.0",
      "definitionVersion": "1.0",
      "variations": [
        {
          "type": "PlainText",
          "value": "Are you sure you want to restart?"
        }
      ]
    }
  ],
  "dialog": {
    "version": "1.0",
    "intents": [
      {
        "name": "Describe",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "Hint",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "No",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "OptionEight",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "OptionFive",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "OptionFour",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "OptionNine",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "OptionOne",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "OptionSeven",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "OptionSix",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "OptionThree",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "OptionTwo",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      },
      {
        "name": "Restart",
        "confirmationRequired": true,
        "prompts": {
          "confirm": "Confirm.Intent-Restart"
        },
        "slots": []
      },
      {
        "name": "Yes",
        "confirmationRequired": false,
        "prompts": {},
        "slots": []
      }
    ]
  }
}
```


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
            "instructions": "To go North, say 1. To wait and see what happens, say 2.",
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
            "instructions": "To go back South, say 1. To proceed East, say 2.",
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
            "instructions": "Say \"restart\" to start over or \"quit\" to leave.",
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
            "instructions": "Say \"restart\" to start over or \"quit\" to leave.",
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