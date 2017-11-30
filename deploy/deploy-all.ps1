[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True)]
  [string]$SubscriptionName,
  
  [Parameter(Mandatory=$True)]
  [string]$RGName,

  [Parameter(Mandatory=$True)]
  [string]$StorageAccountNameDeploy,
  
  [Parameter(Mandatory=$True)]
  [string]$Location,

  [Parameter(Mandatory=$True)]
  [string]$KeyVaultRGName,
	
  [Parameter(Mandatory=$True)]
  [string]$KeyVaultName,
	
  [Parameter(Mandatory=$True)]
  [string]$ResourcePrefix,

  [Parameter(Mandatory=$True)]
  [string]$ClusterName,

  [Parameter(Mandatory=$True)]
  [string]$ServiceBusNamespaceName,

  [Parameter(Mandatory=$True)]
  [string]$IotHubName

)

Select-AzureRmSubscription -SubscriptionName $SubscriptionName
Write-Host "Selected subscription: $SubscriptionName"
$scriptDir = Split-Path $MyInvocation.MyCommand.Path 

# Find existing or deploy new Resource Group:
$rg = Get-AzureRmResourceGroup -Name $RGName -ErrorAction SilentlyContinue
if (-not $rg)
{
    New-AzureRmResourceGroup -Name "$RGName" -Location "$Location"
    Write-Host "New resource group deployed: $RGName"   
}
else{ Write-Host "Resource group found: $RGName"}

# Create Storage Account if not exists yet
$storageAccount = Get-AzureRmStorageAccount -ResourceGroupName $RGName -Name $StorageAccountNameDeploy -ErrorAction SilentlyContinue
if(!$storageAccount)
{
  $storageAccount = New-AzureRmStorageAccount -ResourceGroupName $RGName -Name $StorageAccountNameDeploy -Location $Location -SkuName "Standard_LRS"
  Write-Host "New storage account created: $StorageAccountNameDeploy"
}
else{ Write-Host "Storage account found: $StorageAccountNameDeploy"}

$ctx = $storageAccount.Context

# Create container to upload templates towards
$templatesContainerName = "temptemplates"
$templatesContainerUri = "https://$StorageAccountNameDeploy.blob.core.windows.net/$templatesContainerName"

# Upload main template:
& "$scriptDir\copyFilesToAzureStorageContainer.ps1" -LocalPath "$scriptDir\templates\" `
                                   -StorageContainer $templatesContainerName -StorageContext $ctx -CreateStorageContainer  -Recurse -Force

# Create SAS token for the packages container
$templatesContainerSas = New-AzureStorageContainerSASToken -Context $ctx -Name $templatesContainerName -Permission r -ExpiryTime (Get-Date).AddHours(4)

Write-Host "Sas for templates: $templatesContainerSas"

$rootTemplateUri = (Get-AzureStorageBlob -Blob "azuredeploy.json" -Container $templatesContainerName -Context $ctx).ICloudBlob.Uri.AbsoluteUri

Write-Output "Root template SAS - $rootTemplateUri"

# The only way to pass secure parameters, stored in Key Vault is through a parameters file.  
# See: https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-manager-keyvault-parameter
# Create params file temporary for pointing to the secrets in Key Vault (*.tmp.json is excluded in .gitignore):
$azureRmContext = Get-AzureRmContext
$subscriptionId = $azureRmContext.Subscription.SubscriptionId
$keyVaultId = "/subscriptions/$subscriptionId/resourceGroups/$KeyVaultRGName/providers/Microsoft.KeyVault/vaults/$KeyVaultName"
$clustercertificate = Get-AzureKeyVaultSecret -VaultName $KeyVaultName -Name 'clustercertificate'
$clusterurl = $clustercertificate.Id
Write-Output "clusterurl: " $clusterurl
$paramsFile = @{
    '$schema' = "https://schema.management.azure.com/schemas/2015-01-01/deploymentParameters.json#"
    contentVersion = "1.0.0.0"
    parameters = @{
        deploymentId = @{
           value = "$ResourcePrefix"
        }
        'clusterName' =  @{
            value = "$ClusterName"
            }
        'adminUsername' =  @{
         reference = @{
           keyVault = @{
             id = $keyVaultId
           }
           secretName = 'clusterAdminUsername'
         }
       }
        'adminPassword' =  @{
         reference = @{
           keyVault = @{
             id = $keyVaultId
           }
           secretName = 'clusterAdminPassword'
         }
       }
        'certificateThumbprint' =  @{
          value = "D07E8C71FBD5F793B010D78B4A959FE7D8EC9214"
        }
        'sourceVaultResourceId' =  @{
          value = "$keyVaultId"
        }
        'certificateUrlValue' = @{
          value = "$clusterurl"
        }    
        'serviceBusNamespaceName' = @{
          value = $ServiceBusNamespaceName
        }
        'iotHubName' = @{
          value = $IotHubName
        }
		'_artifactsLocation' = @{
          value = $templatesContainerUri
        }

        '_artifactsSAS' = @{
          value = $templatesContainerSas
        }   
    }       
}

$paramsFilePath = "$scriptDir\..\..\azuredeploy.parameters.json"
Write-Host "Temp params file to be written to: $paramsFilePath"
$paramsFile | ConvertTo-Json -Depth 5 | Out-File $paramsFilePath

# Deploy ARM template
New-AzureRmResourceGroupDeployment -Verbose -Force -ErrorAction Stop `
   -Name "iotdemodeploy" `
   -ResourceGroupName $RGName `
   -TemplateFile "$scriptDir/templates/azuredeploy.json" `
   -TemplateParameterFile $paramsFilePath 


 Remove-Item -Path $paramsFilePath