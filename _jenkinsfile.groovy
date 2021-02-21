@Library( "X13JenkinsLib" )_

pipeline
{
    agent
    {
        label "master"
    }
    environment
    {
        DOTNET_CLI_TELEMETRY_OPTOUT = 'true'
        DOTNET_NOLOGO = 'true'
    }
    options
    {
        skipDefaultCheckout( true );
    }
    stages
    {
        stage( 'clean' )
        {
            steps
            {
                cleanWs();
            }
        }
        stage( 'checkout' )
        {
            steps
            {
                checkout scm;
            }
        }

        stage( 'In Docker' )
        {
            agent
            {
                docker
                {
                    image 'mcr.microsoft.com/dotnet/sdk:3.1'
                    args "-e HOME='${env.WORKSPACE}'"
                    reuseNode true
                }
            }
            stages
            {
                stage( 'prepare' )
                {
                    steps
                    {
                        sh 'dotnet tool update Cake.Tool --tool-path ./Cake'
                        sh './Cake/dotnet-cake ./checkout/build.cake --showdescription'
                    }
                }
            
                stage( 'build' )
                {
                    steps
                    {
                        sh './Cake/dotnet-cake ./checkout/build.cake --target=build_pretzel'
                        sh './Cake/dotnet-cake ./checkout/build.cake --target=generate_rss_feeds'
                        sh './Cake/dotnet-cake ./checkout/build.cake --target=generate'
                    }
                }
            }
        }
    
        stage( 'deploy' )
        {
            steps
            {
                withCredentials(
                    [sshUserPrivateKey(
                        credentialsId: "shendrick.net",
                        usernameVariable: "SSHUSER",
                        keyFileVariable: "WEBSITE_KEY" // <- Note: WEBSITE_KEY must be in all quotes below, or rsync won't work if the path has whitespace.
                    )]
                )
                {
                    script
                    {
                        String verbose = "-v"; // Make "-v" for verbose mode.
                        String sshOptions = "-o BatchMode=yes -o StrictHostKeyChecking=accept-new -i \\\"${WEBSITE_KEY}\\\"";
                        sh "cd ./checkout && rsync --rsh=\"ssh ${verbose} ${sshOptions}\" -az --delete --exclude \".well-known\" ./_site ${SSHUSER}@sites.meditationenthusiasts.org:sites_upload";
                    }
                }
            }
        }
    }
    post
    {
        fixed
        {
            X13SendToTelegramWithCredentials(
                message: "${BUILD_TAG} has been fixed!  See: ${BUILD_URL}",
                botCredsId: "telegram_bot",
                chatCredsId: "telegram_chat_id"
            );
        }
        failure
        {
            X13SendToTelegramWithCredentials(
                message: "${BUILD_TAG} has failed.  See: ${BUILD_URL}",
                botCredsId: "telegram_bot",
                chatCredsId: "telegram_chat_id"
            );
        }
    }
}

