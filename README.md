![Alexa-AdventureBot](assets/images/Alexa-AdventureBot.png)

# λ# Alexa AdventureBot Team Hackathon Challenge
AdventureBot is an [Amazon Alexa Skill](https://developer.amazon.com/alexa-skills-kit) for powering voice-based adventures on Alexa-enabled devices. AdventureBot includes a game library, an AWS lambda function, a command line utility, and an Alexa Skill definition to get you going as quickly as possible.

### Pre-requisites
The following tools and accounts are required to complete these instructions.

* [Install .NET Core 2.1](https://www.microsoft.com/net/download)
* [Install AWS CLI](https://aws.amazon.com/cli/)
* [Sign-up for an AWS account](https://aws.amazon.com/)
* [Sign-up for an Amazon developer account](https://developer.amazon.com/alexa)
* [Install MindTouch LambdaSharp Tool](https://github.com/LambdaSharp/LambdaSharpTool)

### Running the AdventureBot command line app
1. Restore solution packages: `dotnet restore`
2. Change folder the command line project: `cd AdventureBot.Cli`
3. Run app with a sample file: `dotnet run ../assets/sample-adventure.json`

## LEVEL 0 - Setup λ# Tool

* Clone λ# v0.2 from https://github.com/LambdaSharp/LambdaSharpTool
* Follow the setup instructions at https://github.com/LambdaSharp/LambdaSharpTool/tree/master/Bootstrap

## LEVEL 1 - Deploy AdventureBot Lambda Function

### Create `lambdasharp` AWS Profile
The project uses by default the `lambdasharp` profile. Follow these steps to setup a new profile if need be.

1. Create a `lambdasharp` profile: `aws configure --profile lambdasharp`
2. Configure the profile with the AWS credentials you want to use
3. **NOTE**: AWS Lambda function for Alexa Skills must be deployed in `us-east-1`

### Deploy AdventureBot
The AdventureBot code is packaged as a λ# deployment, which streamlines the creating and uploading of assets for serverless applications.

1. Open a shell and switch to the git checkout folder
2. Run the λ# tool to deploy AdventureBot: `lash deploy --profile lambdasharp --tier test`

## LEVEL 2 - Setup AdventureBot Alexa Skill
The following steps set up the Alexa Skill with an invocation name, a predefined set of voice commands, and associates it with the AdventureBot lambda function.

1. [Log into the Amazon Developer Console](https://developer.amazon.com/home.html)
2. Click on the `ALEXA` tab
3. Click on Alexa Skill Kit `Get Started`
4. Click `Add a New Skill`
5. *Skill Information*
    1. Under name put: `AdventureBot`
    2. Under invocation name put: `Adventure Bot`
    3. Click `Save`
    4. Click `Next`
6. *Interaction Model*
    1. Click `Launch Skill Builder`
    2. Click `Discard` to proceed
    3. Click `</> Code` in left navigation
    4. Upload `assets/alexa-skill.json` file
    5. Click `Apply Changes`
    6. Click `Build Mode` in the toolbar
    7. Click `Configuration`
7. *Configuration*
    1. Select `AWS lambda ARN (Amazon Resource Name)`
    2. Select `North America`
    3. Paste in the AWS lambda function ARN: `arn:aws:lambda:us-east-1:******:function:test-AdventureBot-Alexa` (**NOTE:** the missing account ID will be provided during the challenge)
    4. Click `Next`
8. **Congratulations!!** Your Alexa Skill is now available on all your registered Alexa-devices, including the Amazon mobile app. Give it a whirl!
    * For Alexa devices, say: `Alexa, open Adventure Bot`
    * For the Amazon mobile app, click the microphone icon, and say: `open Adventure Bot`
    * Then say, `describe`, `hint`, `yes`, or any other custom and built-in intents.
    * Say `quit` to exit the skill.

### LEVEL 3 - Notify Yourself When Someone Completes Your Adventure
This part is left as an exercise to the reader.

Modify AdventureBot so that it sends out a SNS message when a player completes an adventure. Track how long it took for the player to complete, how many place where visited, and any other statistics you think would be insightful to understand your players.

### BOSS LEVEL - Handle new Players vs. Returning Players differently
This part is left as an exercise to the reader.

AdventureBot uses Alexa session state to track players through their exploration. However, when the session ends, the player state is lost. Add code to AdventureBot to store the player's state in DynamoDB. Detect at the beginning of a new session if the player has an unfinished adventure and offer to resume or restart instead..

### BONUS LEVEL - Showcase your own Adventure!
This part is left as an exercise to the reader.

Create your own adventure and showcase it!

## APPENDIX A - AdventureBot File Format
The AdventureBot uses a simple JSON file to define places. Each place has a description and a list of choices that are available to the player.

### Main
The main object has only one field called `"places"`.

* `"places"`: Map of place IDs to place objects. This map must contain a place called `"start"`.

```
{
    "places": { ... }
}
```

### Place
The place object has multiple fields. All of them are required.

* `"description"`: Text describing the place/situation the player is in. This text is automatically read when the player first enters a place and can be repeated with the built-in *describe* command.
* `"instructions"`: Text describing the actions the player can provide. This text is automatically read when the player first enters a place and can be repeated with the built-in *help* command.
* `"finished"`: (optional) Boolean indicating that the place marks the end of an adventure. The value is `false` by default.
* `"choices"`: Map of choices to actions the player can make.

```
{
    "description": "You are in a room.",
    "instructions": "To go North, say 1.",
    "choices": { ... }
}
```

### Choices
The choice object associates a command with zero or more actions. The field name must be one of the recognized commands:
* `"1"` through `"9"`
* `"yes"` and `"no"`
* `"help"`
* `"hint"`
* `"restart"`
* `"quit"`

```
{
    "yes": [ ... ],
    "no": [ ... ]
}
```

### Actions
The action object associates an action with an argument. The field name must be one of the recognized actions:

* `"say"`: Says one or more sentences.
* `"pause"`: Pause the output for a while.
* `"play"`: Play an MP3 file.

#### Say Action
The say action converts text into speech.

```
{
    "say": [ "You open the door." ]
}
```

#### Pause Action
The pause action is delays further speech for the specified duration in seconds.

```
{
    "pause": [ 0.5 ]
}
```

#### Play Action
The play action plays back an MP3 file. Note the MP3 file must satisfy the following conditions:
* The MP3 must be hosted at an Internet-accessible HTTPS endpoint. HTTPS is required, and the domain hosting the MP3 file must present a valid, trusted SSL certificate. Self-signed certificates cannot be used.
* The MP3 must not contain any customer-specific or other sensitive information.
* The MP3 must be a valid MP3 file (MPEG version 2).
* The audio file cannot be longer than ninety (90) seconds.
* The bit rate must be 48 kbps. Note that this bit rate gives a good result when used with spoken content, but is generally not a high enough quality for music.
* The MP3 sample rate must be 16000 Hz.

A good source of free samples can be found at [SoundEffects+](https://www.soundeffectsplus.com/).

Alexa compatible MP3 can be produced with the `ffmpeg` utility:
`ffmpeg -i <source-file> -ac 2 -codec:a libmp3lame -b:a 48k -ar 16000 <destination-file>`

```
{
    "play": [ "https://example.org/door-close.mp3" ]
}
```

### Acknowledgements
This challenge was made possible by the excellent of Tim Heuer who wrote the outstanding [Alexa.NET](https://github.com/timheuer/alexa-skills-dotnet/) library!

### Copyright & License
* Copyright (c) 2017-2018 Steve Bjorg
* MIT License
