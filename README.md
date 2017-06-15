# λ# AdventureBot - June 2017 Hackathon
AdventureBot is an [Amazon Alexa Skill](https://developer.amazon.com/alexa-skills-kit) for powering voice-based adventures on Alexa-enabled devices. AdventureBot includes a game library, an AWS Lambda function, a command line utility, and an Alexa Skill definitionn to get you going as quickly as possible.

## Pre-requesites
The following tools and accounts are required to complete these instructions.

* [Install .NET Core 1.x](https://www.microsoft.com/net/core)
* [Install AWS CLI](https://aws.amazon.com/cli/)
* [Sign-up for an AWS account](https://aws.amazon.com/)
* [Sign-up for an Amazon developer account](https://developer.amazon.com/)

## Running the AdventureBot command line app
1. Restore solution packages: `dotnet restore`
2. Change folder the command line project: `cd AdventureBot.Cli`
3. Run app with a sample file: `dotnet run ../assets/sample-adventure.json`

## Setting up the Alexa Skill

### 1) `lambdasharp` AWS Profile
The project uses by default the `lambdasharp` profile. Follow these steps to setup a new profile if need be.

1. Create a `lambdasharp profile`: `aws configure --profile lambdasharp`
2. Configure the profile with the AWS credentials you want to use
3. **NOTE**: AWS Lambda function for Alexa Skills must be hosted in `us-east-1`

### 2) Create IAM role for the AdventureBot AWS Lambda function
The AWS Lambda function requires an IAM role to access CloudWatchLogs, S3, SNS, and DynamoDB. You can create the `LambdaSharp-AdventureBotAlexa` role via the [AWS Console](https://console.aws.amazon.com/iam/home) or use the executing [AWS CLI](https://aws.amazon.com/cli/) commands.
```
aws iam create-role --profile lambdasharp --role-name LambdaSharp-AdventureBotAlexa --assume-role-policy-document file://assets/lambda-role-policy.json
aws iam attach-role-policy --profile lambdasharp --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/AmazonS3FullAccess
aws iam attach-role-policy --profile lambdasharp --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/CloudWatchLogsFullAccess
aws iam attach-role-policy --profile lambdasharp --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/AmazonSNSFullAccess
aws iam attach-role-policy --profile lambdasharp --role-name LambdaSharp-AdventureBotAlexa --policy-arn arn:aws:iam::aws:policy/AmazonDynamoDBFullAccess
```

### 3) Upload AdventureBot JSON file
The AdventureBot AWS Lambda function reads the adventure definition from a JSON file that must be uploaded to S3. Follow these steps to create a new bucket and upload a sample adventure file.

1. Create an S3 bucket (e.g. `lambdasharp`)
2. Create an `AdventureBot` folder
3. Upload `assets/sample-adventure.json` file

### 4) Publish the AdventureBot AWS Lambda function
The AdventureBot AWS Lambda function needs to be compiled and published to AWS `us-east-1`. The default publishing settings are in `aws-lambda-tools-defaults.json` file and assume the `lambdasharp` profile. Once published, the AWS Lambda function needs to be configured to be ready for invocation by the Alexa Skill.

1. Change folder to the lambda function: `cd AdventureBot.Alexa`
2. Publish the lambda function: `dotnet lambda deploy-function`
3. [Go to the published lambda function in the console](https://console.aws.amazon.com/lambda/home?region=us-east-1#/functions/LambdaSharp-AdventureBotAlexa?tab=code)
4. Copy the AWS Lambda function ARN for later (e.g. `arn:aws:lambda:us-east-1:******:function:LambdaSharp-AdventureBotAlexa`)
5. Under `Code` > `Environment Variables`
    1. Add key: `adventure_file`
    2. Add value pointing to the JSON file (replace with your bucket name and file path): `s3://lambdasharp/AdventureBot/sample-adventure.json`
    3. Clic `Save`
6. Under `Triggers`
    1. Click `Add Trigger`
    2. Select `Alexa Skills Kit`

### 5) Create AdventureBot Alexa Skill
The following steps set up the Alexa Skill with an invocation name, a predefiend set of voice commands, and associates it with the AdventureBot AWS Lambda function.

1. [Log into the Amazon Developer Console](https://developer.amazon.com/home.html)
2. Click on the `ALEXA` tab
3. Click on Alexa Skill Kit `Get Started`
4. Click `Add a New Skill`
5. *Skill Information*
    1. Under name put: `AdventureBot`
    2. Under invocation name put: `Adventure Bot`
    3. Click `Save`
    4. Clikc `Next`
6. *Interaction Model*
    1. Click `Launch Skill Builder`
    2. Click `Discard` to proceed
    3. Click `</> Code` in left navigation
    4. Upload `assets/alexa-skill.json` file
    5. Click `Apply Changes`
    6. Click `Build Mode` in the toolbar
    7. Click `Configuration`
7. *Configuration*
    1. Select `AWS Lambda ARN (Amazon Resource Name)`
    2. Select `North America`
    3. Paste in the AWS Lambda function ARN (e.g. `arn:aws:lambda:us-east-1:******:function:LambdaSharp-AdventureBotAlexa`)
    4. Click `Next`
8. **Congratulations!!** Your Alexa Skill is now available on all your registerd Alexa-devices, including the Amazon mobile app. Give it a whirl!
    * For Alexa devices, say: `Alexa, open Adventure Bot`
    * For the Amazon mobile app, click the microphone icon, and say: `open Adventure Bot`


# TODO TODO TODO TODO TODO TODO

## Commands
* 1 through 9: are player choices
* yes/no
* help
* hint
* restart
* quit

## Sample File
The AdventureBot uses a simple JSON file to define places with available player choices. The starting place is always called `start`.

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