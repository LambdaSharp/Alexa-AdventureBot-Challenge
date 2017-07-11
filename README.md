# λ# AdventureBot - June 2017 Team Hackathon Challenge
AdventureBot is an [Amazon Alexa Skill](https://developer.amazon.com/alexa-skills-kit) for powering voice-based adventures on Alexa-enabled devices. AdventureBot includes a game library, an AWS lambda function, a command line utility, and an Alexa Skill definition to get you going as quickly as possible.

### Pre-requisites
The following tools and accounts are required to complete these instructions.

* [Install .NET Core 1.x](https://www.microsoft.com/net/core)
* [Install AWS CLI](https://aws.amazon.com/cli/)
* [Sign-up for an AWS account](https://aws.amazon.com/)
* [Sign-up for an Amazon developer account](https://developer.amazon.com/)

### Running the AdventureBot command line app
1. Restore solution packages: `dotnet restore`
2. Change folder the command line project: `cd AdventureBot.Cli`
3. Run app with a sample file: `dotnet run ../assets/sample-adventure.json`

## LEVEL 1 - Setup AdventureBot Alexa Skill
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
    3. Paste in the AWS lambda function ARN: `arn:aws:lambda:us-east-1:******:function:LambdaSharp-AlexaEcho` (**NOTE:** the missing account ID will be provided during the challenge)
    4. Click `Next`
8. **Congratulations!!** Your Alexa Skill is now available on all your registered Alexa-devices, including the Amazon mobile app. Give it a whirl!
    * For Alexa devices, say: `Alexa, open Adventure Bot`
    * For the Amazon mobile app, click the microphone icon, and say: `open Adventure Bot`
    * Then say, `describe`, `hint`, `yes`, or any other custom and built-in intents.
    * Say `quit` to exit the skill.

## LEVEL 2 - Deploy AdventureBot Lambda Function

### Create `lambdasharp` AWS Profile
The project uses by default the `lambdasharp` profile. Follow these steps to setup a new profile if need be.

1. Create a `lambdasharp` profile: `aws configure --profile lambdasharp`
2. Configure the profile with the AWS credentials you want to use
3. **NOTE**: AWS Lambda function for Alexa Skills must be deployed in `us-east-1`

### Create `LambdaSharp-AdventureBotAlexa` role for the lambda function
The `LambdaSharp-AdventureBotAlexa` lambda function requires an IAM role to access CloudWatchLogs, S3, SNS, and DynamoDB. You can create the `LambdaSharp-AdventureBotAlexa` role via the [AWS Console](https://console.aws.amazon.com/iam/home) or use the executing [AWS CLI](https://aws.amazon.com/cli/) commands.

```shell
aws iam create-role --profile lambdasharp --role-name LambdaSharp-AdventureBotAlexa --assume-role-policy-document file://assets/lambda-role-policy.json
aws iam attach-role-policy --profile lambdasharp --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/AmazonS3FullAccess
aws iam attach-role-policy --profile lambdasharp --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/CloudWatchLogsFullAccess
aws iam attach-role-policy --profile lambdasharp --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/AmazonSNSFullAccess
aws iam attach-role-policy --profile lambdasharp --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/AmazonDynamoDBFullAccess
```

### Upload AdventureBot JSON file
The AdventureBot lambda function reads the adventure definition from a JSON file that must be uploaded to S3. Follow these steps to create a new bucket and upload a sample adventure file.

1. Create an S3 bucket or reuse an existing one (e.g. `my-adventurebot-bucket`)
2. Create an `AdventureBot` folder
3. Upload `assets/sample-adventure.json` file into the `AdventureBot` folder

### Publish the AdventureBot lambda function
The AdventureBot lambda function needs to be compiled and published to AWS `us-east-1`. The default publishing settings are in `aws-lambda-tools-defaults.json` file and assume the `lambdasharp` profile. Once published, the lambda function needs to be configured to be ready for invocation by the Alexa Skill.

1. Change folder to the lambda function: `cd AdventureBot.Alexa`
2. Publish the lambda function: `dotnet lambda deploy-function`
3. [Go to the published lambda function in the console](https://console.aws.amazon.com/lambda/home?region=us-east-1#/functions/LambdaSharp-AdventureBotAlexa?tab=code)
4. Copy the AWS Lambda function ARN for later (e.g. `arn:aws:lambda:us-east-1:******:function:LambdaSharp-AdventureBotAlexa`)
5. Under `Code` > `Environment Variables`
    1. Add key: `adventure_file`
    2. Add value pointing to the JSON file (replace with your bucket name and file path): `s3://lambdasharp/AdventureBot/sample-adventure.json`
    3. Click `Save`
6. Under `Triggers`
    1. Click `Add Trigger`
    2. Select `Alexa Skills Kit`
    3. Click `Submit` (**NOTE**: if `Submit` is grayed out, select `Alexa Smart Home` trigger instead and then select `Alexa Skills Kit` trigger again)

#### Update the Alexa kill
Finally, the Alexa Sill needs to be updated to point to the AdventureBot lambda function.

(e.g. `arn:aws:lambda:us-east-1:******:function:LambdaSharp-AdventureBotAlexa`)

1. [Log into the Amazon Developer Console](https://developer.amazon.com/home.html)
2. Click on the `ALEXA` tab
3. Click on Alexa Skill Kit `Get Started`
4. Click on `AdventureBot`
7. Click on `Configuration`
3. Update the AWS lambda function with the ARN you copied from the AdventureBot lambda function: (e.g. `arn:aws:lambda:us-east-1:******:function:LambdaSharp-AdventureBotAlexa`)
4. Click `Save`

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

```json
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

```json
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

```json
{
    "yes": [ ... ],
    "no": [ ... ]
}
```

### Actions
The action object associates an action with an argument. The field name must be one of the recognized actions:

* `"goto"`: Moves the player to a new place.
* `"say"`: Says one or more sentences.
* `"pause"`: Pause the output for a while.
* `"play"`: Play an MP3 file.

#### Say Action
The say action converts text into speech.

```json
{
    "say": [ "You open the door." ]
}
```

#### Pause Action
The pause action is delays further speech for the specified duration in seconds.

```json
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

```json
{
    "play": [ "https://example.org/door-close.mp3" ]
}
```

### Acknowledgements
This challenge was made possible by the excellent of Tim Heuer who wrote the outstanding [Alexa.NET](https://github.com/timheuer/alexa-skills-dotnet/) library!

### Copyright & License
* Copyright (c) 2017 Steve Bjorg
* MIT License
