// The three container apps: RabbitMQ, the Worker and the Api. Images come
// from GitHub's container registry, where CI publishes them.
param location string
param tags object
param environmentId string

@description('The identity the Api and the Worker run as.')
param identityId string

@description('Its client ID, which tells Azure.Identity which identity to ask a token for.')
param identityClientId string

@description('A tag CI published: "sha-" and the first 7 characters of a master commit.')
param imageTag string

param databaseHost string
param databaseUserName string
param appInsightsConnectionString string

@description('The apps subnet\'s range: requests reach the Api from the environment\'s proxy, inside it.')
param appsSubnetPrefix string

var imageRegistry = 'ghcr.io/gollaf'
var rabbitMqUser = 'financetracker'
var rabbitMqPort = 5672

@description('The password of the RabbitMQ user; see main.bicep.')
@secure()
param rabbitMqPassword string

// No password: the apps sign in with a token for the identity, whose name
// is its PostgreSQL role. EF Core's migrations create the database.
var connectionString = 'Host=${databaseHost};Database=financetracker;Username=${databaseUserName};Ssl Mode=Require'

// Settings the Api, the Worker and the migration step share.
var commonEnv = [
  {
    name: 'ConnectionStrings__FinanceTracker'
    value: connectionString
  }
  {
    // The application asks Entra ID for a database token instead of
    // reading a password.
    name: 'Database__UseManagedIdentity'
    value: 'true'
  }
  {
    // Read by Azure.Identity: which of the attached identities to use.
    name: 'AZURE_CLIENT_ID'
    value: identityClientId
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsightsConnectionString
  }
  {
    // One JSON object per log line, so the console output can be queried
    // by field in Log Analytics.
    name: 'Logging__Console__FormatterName'
    value: 'json'
  }
  {
    name: 'Logging__Console__FormatterOptions__IncludeScopes'
    value: 'true'
  }
]

// The identity, attached to an app. "All": the init container may use it
// too, which the migration step needs to reach the database.
var identity = {
  type: 'UserAssigned'
  userAssignedIdentities: {
    '${identityId}': {}
  }
}
var identitySettings = [
  {
    identity: identityId
    lifecycle: 'All'
  }
]

// Applies pending migrations and exits, before the app's own container
// starts: the Api image's "migrate" command. Its CPU and memory count
// toward the app's total.
var migrateInitContainer = {
  name: 'migrate'
  image: '${imageRegistry}/finance-tracker-api:${imageTag}'
  args: [
    'migrate'
  ]
  env: concat(commonEnv, [
    {
      name: 'ASPNETCORE_ENVIRONMENT'
      value: 'Production'
    }
  ])
  resources: {
    cpu: json('0.25')
    memory: '0.5Gi'
  }
}

resource rabbitMq 'Microsoft.App/containerApps@2025-07-01' = {
  name: 'finance-tracker-rabbitmq'
  location: location
  tags: tags
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      // Internal TCP: other apps in the environment connect to
      // finance-tracker-rabbitmq:5672; nothing outside can.
      ingress: {
        external: false
        transport: 'tcp'
        targetPort: rabbitMqPort
        exposedPort: rabbitMqPort
      }
      secrets: [
        {
          name: 'rabbitmq-password'
          value: rabbitMqPassword
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'rabbitmq'
          // Docker's official image from its mirror on Amazon ECR Public,
          // which has no anonymous pull limits like Docker Hub's.
          image: 'public.ecr.aws/docker/library/rabbitmq:4'
          // RabbitMQ throttles publishers above a memory watermark and
          // can't always detect a container's limit, so it is set to
          // about 60% of the container's memory before the server starts.
          command: [
            'sh'
            '-c'
            'echo \'vm_memory_high_watermark.absolute = 600Mi\' > /etc/rabbitmq/conf.d/90-memory.conf && exec docker-entrypoint.sh rabbitmq-server'
          ]
          env: [
            {
              name: 'RABBITMQ_DEFAULT_USER'
              value: rabbitMqUser
            }
            {
              name: 'RABBITMQ_DEFAULT_PASS'
              secretRef: 'rabbitmq-password'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              // Up to 2 minutes to start before the replica is replaced.
              type: 'Startup'
              tcpSocket: {
                port: rabbitMqPort
              }
              periodSeconds: 10
              failureThreshold: 12
            }
            {
              type: 'Liveness'
              tcpSocket: {
                port: rabbitMqPort
              }
              periodSeconds: 30
            }
          ]
        }
      ]
      // Exactly one broker. Its storage lasts as long as its replica.
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

resource worker 'Microsoft.App/containerApps@2025-07-01' = {
  name: 'finance-tracker-worker'
  location: location
  tags: tags
  identity: identity
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      identitySettings: identitySettings
      secrets: [
        {
          name: 'rabbitmq-password'
          value: rabbitMqPassword
        }
      ]
      // No ingress: the Worker serves no requests.
    }
    template: {
      initContainers: [
        migrateInitContainer
      ]
      containers: [
        {
          name: 'worker'
          image: '${imageRegistry}/finance-tracker-worker:${imageTag}'
          env: concat(commonEnv, [
            {
              name: 'DOTNET_ENVIRONMENT'
              value: 'Production'
            }
            {
              // The RabbitMQ app's name is its address in the environment.
              name: 'RabbitMq__Host'
              value: rabbitMq.name
            }
            {
              name: 'RabbitMq__UserName'
              value: rabbitMqUser
            }
            {
              name: 'RabbitMq__Password'
              secretRef: 'rabbitmq-password'
            }
          ])
          // With the init container: 0.5 vCPU and 1 GiB, an allowed pair.
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      // Exactly one: the outbox relay must run in a single process. Never
      // zero: the relay polls the database, which no request would wake.
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

resource api 'Microsoft.App/containerApps@2025-07-01' = {
  name: 'finance-tracker-api'
  location: location
  tags: tags
  identity: identity
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      identitySettings: identitySettings
      // Public HTTPS on an address Azure assigns, with Azure's certificate.
      // Plain HTTP is redirected to HTTPS.
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
    }
    template: {
      initContainers: [
        migrateInitContainer
      ]
      containers: [
        {
          name: 'api'
          image: '${imageRegistry}/finance-tracker-api:${imageTag}'
          env: concat(commonEnv, [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ApiDocs__Enabled'
              value: 'true'
            }
            {
              // The init container has already migrated.
              name: 'Database__ApplyMigrationsOnStartup'
              value: 'false'
            }
            {
              // Requests arrive from the environment's proxy, which adds
              // the visitor's address to X-Forwarded-For.
              name: 'ReverseProxy__TrustedNetworks__0'
              value: appsSubnetPrefix
            }
          ])
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                port: 8080
                path: '/health/live'
              }
              periodSeconds: 30
            }
            {
              // No traffic until the database is reachable and migrated.
              type: 'Readiness'
              httpGet: {
                port: 8080
                path: '/health/ready'
              }
              periodSeconds: 10
            }
          ]
        }
      ]
      // Zero when idle, to save credit; the first request after a quiet
      // spell waits for a replica to start. One at most, like the server.
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
