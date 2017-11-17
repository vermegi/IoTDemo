#
# Deploys KeyVault instance in given resource group and populates with given secrets
#

[CmdletBinding()]
Param(
  # Params required for provisioning into the exsiting Resource group:
  [Parameter(Mandatory=$True)]
  [string]$SubscriptionName,
  
  [Parameter(Mandatory=$True)]
  [string]$RGName,
  
  [Parameter(Mandatory=$True)]
  [string]$Location,

 # Params required for KeyVault deployment:
  [Parameter(Mandatory=$True)]
  [string]$KeyVaultName,

 # Params required for KeyVault secret storage:
  [Parameter(Mandatory=$True)]
  [string]$ClusterAdminUsername,

  [Parameter(Mandatory=$True)]
  [string]$ClusterAdminPassword
)

Select-AzureRmSubscription -SubscriptionName $SubscriptionName
Write-Host "Selected subscription: $SubscriptionName"

# Find existing or deploy new Resource Group:
$rg = Get-AzureRmResourceGroup -Name $RGName -ErrorAction SilentlyContinue
if (-not $rg)
{
    New-AzureRmResourceGroup -Name "$RGName" -Location "$Location"
    echo "New resource group deployed: $RGName"   
}
else{ echo "Resource group found: $RGName"}

# Find existing or deploy new Key Vault:
$vault = Get-AzureRmKeyVault -VaultName $KeyVaultName -ErrorAction SilentlyContinue
if (-not $vault)
{
    Write-Host "Creating Key Vault: $KeyVaultName"
    $vault = New-AzureRmKeyVault -VaultName $KeyVaultName `
                             -ResourceGroupName $RGName `
                             -Sku premium `
                             -Location $Location `
                             -EnabledForTemplateDeployment
}
else{echo "Key Vault found: $KeyVaultName - using this instance"}

# Configure Microsoft.CertificateRegistration and Microsoft.Web resource providers
# to grant access to Key Vault, so certs can be deployed and read for web apps
# See sample: https://github.com/Azure/azure-quickstart-templates/tree/master/101-app-service-certificate-standard
Write-Host "Granting Microsoft.CertificateRegistration and Microsoft.Web access to the Key Vault"
Set-AzureRmKeyVaultAccessPolicy -VaultName $KeyVaultName `
   -ServicePrincipalName f3c21649-0979-4721-ac85-b0216b2cf413 `
   -PermissionsToSecrets get,set,delete 
Set-AzureRmKeyVaultAccessPolicy -VaultName $KeyVaultName `
   -ServicePrincipalName abfa0a7c-a6b6-4736-8310-5855508787cd `
   -PermissionsToSecrets get


$ClusterAdminUsernameSecure = ConvertTo-SecureString $ClusterAdminUsername -AsPlainText -Force
$ClusterAdminPasswordSecure = ConvertTo-SecureString $ClusterAdminPassword -AsPlainText -Force

$ClusterAdminUsernameSecret = Set-AzureKeyVaultSecret -VaultName $KeyVaultName -Name "clusterAdminUsername" -SecretValue $ClusterAdminUsernameSecure
$ClusterAdminPasswordSecret = Set-AzureKeyVaultSecret -VaultName $KeyVaultName -Name "clusterAdminPassword" -SecretValue $ClusterAdminPasswordSecure

Write-Host "KeyVault has been created or found and secrets have been stored."
Write-Host "Refer to this Keyvault instance in future scripts as: $KeyVaultName within Resource Group $RGName"
Write-Host ""
Write-Host "KeyVault SecretURI for cluster login: $($vault.ResourceId)/secrets/$($ClusterAdminUsernameSecret.Name)"
Write-Host "KeyVault SecretURI for cluster Password: $($vault.ResourceId)/secrets/$($ClusterAdminPasswordSecret.Name)"
