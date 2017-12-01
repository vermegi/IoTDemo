# IoTDemo

This project sets up a IoT pipeline in Azure, demonstrating a couple of Azure components. It creates for you the necessary infrastructure components (IoTHub, Service Fabric Cluster, CosmosDb, Service Bus, ...). This can be found in de deploy directory of the repo. 

Additionally this repo contains code for getting a pipeling going in ServiceFabric with the Actor model.

This is a work in progress! 

## Getting The Infra Ready

Make sure you have the latest Azure Powershell stuff installed. 
Clone this project, open the deploy folder in Powershell: 

1. Login to your Azure subscription using Login-AzureRmAccount
2. run CreateKeyvault and supply the necessary parameters

eg.: 

```
.\CreateKeyvault.ps1 -SubscriptionName 'your subscription' -RGName 'your resourcegroup' -Location 'West Europe' -KeyVaultName 'name of the new keyvault' -ClusterAdminUsername 'theadmin' -ClusterAdminPassword 'the admin password'
```

This creates a keyvault and puts your servicefabric cluster password in it.

3. run New-ServiceFabricClusterCertificate.ps1 and supply the necessarry parameters

eg.: 

```
.\New-ServiceFabricClusterCertificate.ps1 -Password 'cert password' -CertDNSName 'something.com' -KeyVaultName 'name of the keyvault you just created' -KeyVaultSecretName 'clustercertificate'
```

The ARM templates are based on the Azure Quickstart Templates, one of which is [this one](https://github.com/Azure/azure-quickstart-templates/tree/master/service-fabric-secure-cluster-5-node-1-nodetype). The certificate stuff is also explained [here](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-cluster-creation-via-portal).

4. run SetupApplication.ps1 in the AADTool directory. 

eg.: 

```
.\SetupApplications.ps1 -TenantId 'your tenant ID' -ClusterName 'name that you're going to give to your cluster, eg. mycluster' -WebApplicationReplyUrl 'https://mycluster.westeurope.cloudapp.azure.com:19080/Explorer/index.html'
```

This process is also explained [here](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-cluster-creation-via-arm#set-up-azure-active-directory-for-client-authentication).

You can get your tenant Id by running Get-AzureRmSubscription. 
Take a note of the output of this script, it is needed in the next step. 

5. run deploy-all.ps1 and supply the necessarry parameters

eg.: 

```
.\deploy-all.ps1 -SubscriptionName 'your subscription' `
                    -RGName 'your resourcegroup' `
                    -StorageAccountNameDeploy 'name of a temporary storage account that will get created' `
                    -Location 'West Europe' `
                    -KeyVaultRGName 'resource group of your keyvault' `
                    -KeyVaultName 'name of the keyvault you just created' `
                    -ResourcePrefix 'prefix used for all resources, don t make this too long' `
                    -ClusterName 'name for your new SF cluster' `
                    -ServiceBusNamespaceName 'namespace for your new servicebus' `
                    -IotHubName 'name of your new IoTHub' `
                    -TenantId 'tenant ID' `
                    -ClusterApplication 'cluster application ID' `
                    -ClientApplication 'client application ID'
```

If all goes well, you should now (it takes some time, so be patient) have the necessary resources setup in Azure. 


## Getting the code in Azure

Open the src/IoTDemoApp.sln in Visual Sturio 2017. Right mouse click on the IoTDemoApp, select 'Publish'. In the popup window, select your newly created cluster and click Ok. This should now publish all services to the ServiceFabric cluster. 

You're up and running. 

And again: WiP :) 
