pipeline {
    agent any

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        CONFIGURATION = 'Release'
        IMAGE_NAME = 'phonestore-api'
        IMAGE_TAG = "${env.BUILD_NUMBER}"
    }

    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timestamps()
        timeout(time: 30, unit: 'MINUTES')
    }

    stages {
        stage('Checkout') {
            steps {
                echo '=== [N2LHTBLTT-37] Checking out repository ==='
                checkout scm
            }
        }

        stage('Restore Dependencies') {
            steps {
                echo '=== Restoring .NET NuGet packages ==='
                bat 'dotnet restore PhoneStore.slnx'
            }
        }

        stage('Build Solution') {
            steps {
                echo '=== Building PhoneStore Solution (Release) ==='
                bat "dotnet build PhoneStore.slnx --configuration ${CONFIGURATION} --no-restore"
            }
        }

        stage('Run Unit & Architecture Tests') {
            steps {
                echo '=== Running Unit and Architecture Tests ==='
                bat "dotnet test tests/PhoneStore.UnitTests/PhoneStore.UnitTests.csproj --configuration ${CONFIGURATION} --no-build --logger trx;LogFileName=unit-tests.trx"
                bat "dotnet test tests/PhoneStore.ArchitectureTests/PhoneStore.ArchitectureTests.csproj --configuration ${CONFIGURATION} --no-build --logger trx;LogFileName=arch-tests.trx"
            }
        }

        stage('Docker Build') {
            steps {
                echo '=== Building Multi-stage Docker Image ==='
                bat "docker build -f docker/Dockerfile.api -t ${IMAGE_NAME}:${IMAGE_TAG} -t ${IMAGE_NAME}:latest ."
            }
        }

        stage('Deploy Staging') {
            steps {
                echo '=== Deploying via Docker Compose ==='
                bat 'docker compose -f docker/docker-compose.prod.yml up -d'
            }
        }
    }

    post {
        always {
            echo '=== Archiving Test Results ==='
            junit allowEmptyResults: true, testResults: '**/TestResults/*.trx'
            cleanWs()
        }
        success {
            echo "Pipeline completed successfully for build #${env.BUILD_NUMBER}!"
        }
        failure {
            echo "Pipeline failed on build #${env.BUILD_NUMBER}. Please check logs."
        }
    }
}
