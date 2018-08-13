![Alexa-AdventureBot](assets/images/Alexa-AdventureBot.png)

# λ# Alexa AdventureBot Team Hackathon Challenge
AdventureBot is an [Amazon Alexa Skill](https://developer.amazon.com/alexa-skills-kit) for powering voice-based adventures on Alexa-enabled devices. AdventureBot includes a game library, an AWS lambda function, a command line utility, and an Alexa Skill definition to get you going as quickly as possible.

## Pre-requisites
The following tools and accounts are required to complete these instructions.

* [Install .NET Core 2.1](https://www.microsoft.com/net/download)
* [Install AWS CLI](https://aws.amazon.com/cli/)
* [Sign-up for an AWS account](https://aws.amazon.com/)
* [Sign-up for an Amazon developer account](https://developer.amazon.com/alexa)
* [Setup LambdaSharp Tool](https://github.com/LambdaSharp/LambdaSharpTool)

## LEVEL 0 - Run Command Line Version

1. Clone or download the GitHub repository: `https://github.com/LambdaSharp/Alexa-AdventureBot-Challenge`
1. Change folder the command line project: `cd Alexa-AdventureBot-Challenge/AdventureBot.Cli`
1. Run app with a sample file: `dotnet run ../assets/adventures/my-demo-adventure.yml`
    * **NOTE**: type `quit` to exit

## LEVEL 1 - Deploy AdventureBot on AWS

<details><summary>Set AWS Profile and λ# Tool</summary>

1. If you haven't already done so, configure your AWS profile using: `aws configure`
    * **NOTE**: AWS Lambda functions for Alexa Skills must be deployed in `us-east-1`
1. Verify your λ# tool setup by listing the deployed modules: `lash list`
    * **NOTE**: With the the `LAMBDASHARPTIER` environment variable you can omit the `--tier` command line option.

The following text should appear (or similar):
```
$ lash list
MindTouch LambdaSharp Tool (v0.2) - List LambdaSharp modules

MODULE                        STATUS             DATE
LambdaSharp                   [UPDATE_COMPLETE]  2018-08-06 05:18:40
LambdaSharpS3PackageLoader    [UPDATE_COMPLETE]  2018-08-06 23:23:55

Found 2 modules for deployment tier 'Demo'
```
</details>

<details><summary>Deploy AdventureBot</summary>

The AdventureBot code is packaged as a λ# module, which streamlines the creating, configuring, and uploading of assets for serverless applications.

1. Switch to the git checkout folder
1. Run the λ# tool to deploy AdventureBot: `lash deploy`

Once complete, the λ# tool will have taken care of the following steps for you:
* Create the AdventureBot Lambda function (`AdventureBot.Alexa`)
* Configure the Lambda function to use `my-demo-adventure.yml`
* Create the S3 Bucket to hold the adventure and sound files (`AdventureBucket`)
* Upload the adventure and sound files to the S3 Bucket
* Create a public access policy for the sound files (required by sound playback) (`SoundFilesPolicy`)
* Create the DynamoDB table to hold the player state (`PlayerTable`)
* Create the AdventureBot End-of-Adventure SNS Topic (`AdventureFinishedTopic`)
</details>

<details><summary>Test Lambda Function</summary>

1. Open the AWS Console.
1. Go to the deployed Lambda function.
1. Click on the `Test` button and the top right corner.
1. Add the following test event:
    ```json
    {
    "session": {
        "new": true,
        "sessionId": "amzn1.echo-api.session.123",
        "attributes": {},
        "user": {
        "userId": "amzn1.ask.account.123"
        },
        "application": {
        "applicationId": "amzn1.ask.skill.123"
        }
    },
    "version": "1.0",
    "request": {
        "locale": "en-US",
        "timestamp": "2016-10-27T18:21:44Z",
        "type": "LaunchRequest",
        "requestId": "amzn1.echo-api.request.123"
    },
    "context": {
        "AudioPlayer": {
        "playerActivity": "IDLE"
        },
        "System": {
        "device": {
            "supportedInterfaces": {
            "AudioPlayer": {}
            }
        },
        "application": {
            "applicationId": "amzn1.ask.skill.123"
        },
        "user": {
            "userId": "amzn1.ask.account.123"
        }
        }
    }
    }
    ```
1. Click Save.
1. Click Test.
1. After a few seconds you should see the following log output:
    ```
    START RequestId: 68ae2de4-9f22-11e8-af75-9dfa5dec067f Version: $LATEST
    *** INFO: function age: 00:00:00.2227575 [00:00:00.0032438]
    *** INFO: function invocation counter: 1 [00:00:00.0601929]
    *** INFO: start function initialization [00:00:00.0602282]
    *** INFO: TIER = test [00:00:00.1021029]
    *** INFO: MODULE = AdventureBot [00:00:00.1399067]
    *** INFO: DEADLETTERQUEUE = https://sqs.us-east-1.amazonaws.com/123456789012/Demo-LambdaSharp-DeadLetterQueue-CLS0Z58PDNF7 [00:00:00.1399346]
    *** INFO: LOGGINGTOPIC = arn:aws:sns:us-east-1:123456789012:Demo-LambdaSharp-LoggingTopic-HRHBCQ2CIG5I [00:00:00.1399503]
    *** INFO: GITSHA = 016eb793458915e09e37aa98831696919803b946 [00:00:00.1401559]
    *** INFO: ROLLBAR = DISABLED [00:00:03.1799579]
    *** INFO: ADVENTURE_FILE = s3://demo-adventurebot-adventurebucket-7lvk4gdds7ei/Adventures/my-demo-adventure.yml [00:00:03.3615716]
    *** INFO: SOUND_FILES = https://demo-adventurebot-adventurebucket-7lvk4gdds7ei.s3.amazonaws.com/Sounds/ [00:00:03.3799610]
    *** INFO: end function initialization [00:00:03.3800065]
    *** INFO: no previous state found in player table [00:00:14.2404703]
    *** INFO: new player session started [00:00:14.2598708]
    *** INFO: player status: New [00:00:14.2602678]
    *** INFO: launch [00:00:14.2602949]
    *** INFO: storing state in player table
    {"RecordId":"resume-38ed755cbe3288b2c875a0413d51b683","CurrentPlaceId":"start","Status":1,"Start":"2018-08-13T17:58:13.1550708Z","End":null,"AdventureAttempts":1,"CommandsIssued":2} [00:00:15.0230883]
    *** INFO: invocation completed [00:00:15.4424319]
    END RequestId: 68ae2de4-9f22-11e8-af75-9dfa5dec067f
    REPORT RequestId: 68ae2de4-9f22-11e8-af75-9dfa5dec067f	Duration: 15942.98 ms	Billed Duration: 16000 ms 	Memory Size: 128 MB	Max Memory Used: 42 MB
    ```

</details>

## LEVEL 2 - Setup AdventureBot Alexa Skill

The following steps set up the Alexa Skill with an invocation name, a predefined set of voice commands, and associates it with the AdventureBot lambda function.

<details><summary>Setup Alexa Skill</summary>

1. [Log into the Amazon Developer Console](https://developer.amazon.com/home.html)
1. Click on the `Alexa` logo
1. Click on `Alexa Skill Kit` under _Add Capabilities to Alexa_
1. Click on `Start a Skill`
1. Click on `Create Skill`
1. Enter the skill name: `AdventureBot`
1. Click on `Create Skill`
1. _Interaction Model_
    1. Click on `JSON Editor` in the left navigation
    1. Upload `assets/alexa-skill.json` file
    1. Click `Save Model`
    1. Click `Build Model` (this will take a minutes)
1. _Endpoint_
    1. Select `AWS Lambda ARN` option
    1. Under `Default Region` paste the Lambda ARN: `arn:aws:lambda:us-east-1:******:function:Demo-AdventureBot-Alexa`
        * **NOTE**: the Lambda ARN can be found on the top right corner of the AWS Console after selecting the Lambda function
    1. Click `Save Endpoints`
1. _Test_
    1. Click on `Test` in the top banner
    1. Enable `Test is enabled for this skill`
1. **Congratulations!!** Your Alexa Skill is now available on all your registered Alexa-devices and Alexa-enabled apps.
    * For Alexa devices, say: `Alexa, open Adventure Bot`
    * For the Amazon Alexa app, click the microphone icon, and say: `open Adventure Bot`
    * For the Alexa Developer Console, type in `open adventure bot`
    * Then say, `describe`, `hint`, `yes`, or any other custom and built-in intents.
    * Say `quit` to exit the skill.
</details>

## LEVEL 3 - Notify Yourself When Someone Completes Your Adventure

This part is left as an exercise to the reader.

Modify AdventureBot so that it sends out a SNS message when a player completes an adventure. Track how long it took for the player to complete, how many place where visited, and any other statistics you think would be insightful to understand your players.

## LEVEL 4 - Showcase your own Adventure!

This part is left as an exercise to the reader.

Create your own adventure and showcase it!

## BOSS LEVEL - Handle new Players vs. Returning Players differently

This part is left as an exercise to the reader.

AdventureBot uses Alexa session state to track players through their exploration. However, when the session ends, the player state is lost. Add code to AdventureBot to store the player's state in DynamoDB. Detect at the beginning of a new session if the player has an unfinished adventure and offer to resume or restart instead..

# APPENDIX A - AdventureBot File Format

The AdventureBot uses a simple JSON file to define places. Each place has a description and a list of choices that are available to the player.

## Main

The main object has only one field called `places`.

```yaml
places:
    PlaceDefinition
```

<dl>

<dt><tt>places</tt></dt>
<dd>
Map of place IDs to place objects. This map must contain a place called <tt>start</tt>.

<em>Required</em>: Yes

<em>Type</em>: Map of [Place Definitions](#place)
</dd>

</dl>

## Place
The place object has multiple fields.

```yaml
name:
    description: String
    instructions: String
    choices:
        ChoiceDefinition
```

<dl>

<dt><tt>description</tt></dt>
<dd>
Text describing the place/situation the player is in. This text is automatically read when the player first enters a place and can be repeated with the built-in *describe* command.

<em>Required</em>: Yes

<em>Type</em>: String
</dd>

<dt><tt>instructions</tt></dt>
<dd>
Text describing the actions the player can provide. This text is automatically read when the player first enters a place and can be repeated with the built-in *help* command.


<em>Required</em>: Yes

<em>Type</em>: String
</dd>

<dt><tt>finished</tt></dt>
<dd>
Boolean indicating that the place marks the end of an adventure. The value is <tt>false</tt> by default.

<em>Required</em>: No

<em>Type</em>: Boolean
</dd>

<dt><tt>choices</tt></dt>
<dd>
Map of choices to actions the player can make.

<em>Required</em>: Yes

<em>Type</em>: Map of [Choice Definitions](#choices)
</dd>

</dl>

## Choices

The choice object associates a command with zero or more actions. The field name must be one of the recognized commands:
* `"one"` through `"nine"`
* `"yes"` and `"no"`
* `"help"`
* `"hint"`
* `"restart"`
* `"quit"`

```yaml
yes:
  - ActionDefinition
no:
  - ActionDefinition
```

## Actions

The action object associates an action with an argument. The field name must be one of the recognized actions:

* `"say"`: Says one or more sentences
* `"pause"`: Pause the output for a while
* `"play"`: Play an MP3 file
* `"goto"`: Go to place

### Say Action

The say action converts text into speech.

```yaml
say: You open the door.
```

### Pause Action

The pause action is delays further speech for the specified duration in seconds.

```yaml
pause: 0.5
```


### Play Action

The play action plays back an MP3 file. Note the MP3 file must satisfy certain conditions (see [APPENDIX B - Sound File Format](#appendix-b---sound-file-format)).

```yaml
play: door-close.mp3
```

### Goto Action

The goto action changes the place a player is in. The new place is described at the end of the commands.

```yaml
goto: name-of-place
```

# APPENDIX B - Sound File Format

The MP3 file must satisfy the following conditions:
* The MP3 must be hosted at an Internet-accessible HTTPS endpoint. HTTPS is required, and the domain hosting the MP3 file must present a valid, trusted SSL certificate. Self-signed certificates cannot be used.
* The MP3 must not contain any customer-specific or other sensitive information.
* The MP3 must be a valid MP3 file (MPEG version 2).
* The audio file cannot be longer than ninety (90) seconds.
* The bit rate must be 48 kbps. Note that this bit rate gives a good result when used with spoken content, but is generally not a high enough quality for music.
* The MP3 sample rate must be 16000 Hz.

A good source of free samples can be found at [SoundEffects+](https://www.soundeffectsplus.com/).

Alexa compatible MP3 can be produced with the `ffmpeg` utility:
```
ffmpeg -i <source-file> -ac 2 -codec:a libmp3lame -b:a 48k -ar 16000 <destination-file>
```

# Acknowledgements
This challenge was made possible by the excellent of Tim Heuer who wrote the outstanding [Alexa.NET](https://github.com/timheuer/alexa-skills-dotnet/) library!

# Copyright & License
* Copyright (c) 2018 MindTouch, Inc.
* Apache 2.0
