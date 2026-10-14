# Provisionamento do Azure AI Foundry

Tudo via Azure CLI. Os nomes abaixo são os usados na demo; troque o sufixo do recurso (o nome vira subdomínio e precisa ser único).

```powershell
$SUB = "<id-da-assinatura>"
$RG  = "rg-agente-viagens"
$LOC = "swedencentral"
$ACC = "aif-agente-viagens-tiago"
$PRJ = "agente-viagens"

az login
az account set --subscription $SUB

# 1. Resource group
az group create -n $RG -l $LOC

# 2. Recurso Foundry (AIServices) com gerenciamento de projetos
az cognitiveservices account create -n $ACC -g $RG -l $LOC `
  --kind AIServices --sku S0 --custom-domain $ACC --allow-project-management --yes

# 3. Projeto
az cognitiveservices account project create -n $ACC -g $RG -l $LOC --project-name $PRJ

# 4. Deployment do modelo
az cognitiveservices account deployment create -n $ACC -g $RG `
  --deployment-name gpt-4.1 --model-format OpenAI --model-name gpt-4.1 `
  --model-version 2025-04-14 --sku-name GlobalStandard --sku-capacity 50

# 5. Permissão de inferência para a sua conta (papel "Azure AI User", pelo GUID)
$ME    = az ad signed-in-user show --query id -o tsv
$SCOPE = az cognitiveservices account show -n $ACC -g $RG --query id -o tsv
az role assignment create --assignee-object-id $ME --assignee-principal-type User `
  --role 53ca6127-db72-4b80-b1b0-d745d6d5456d --scope $SCOPE

# 6. Endpoint do projeto -> appsettings.json (Foundry:ProjectEndpoint)
az cognitiveservices account project show -n $ACC -g $RG --project-name $PRJ --query "properties.endpoints"
```

O endpoint tem o formato `https://<recurso>.services.ai.azure.com/api/projects/<projeto>`.
A atribuição de papel pode levar alguns minutos para propagar; até lá o preflight acusa 401/403.

## Problemas conhecidos

- **AADSTS50076 (MFA exigido) nas escritas:** faça login pedindo o MFA explicitamente:
  `az login --tenant <tenant> --scope "https://management.core.windows.net//.default" --claims-challenge "eyJhY2Nlc3NfdG9rZW4iOnsiYWNycyI6eyJlc3NlbnRpYWwiOnRydWUsInZhbHVlcyI6WyJwMSJdfX19"`
- **Login em loop com conta pessoal (hotmail/outlook) no Windows:** desligue o broker WAM e use o navegador:
  `az config set core.enable_broker_on_windows=false`
- **`Role 'Azure AI User' doesn't exist`:** use o GUID do papel, como no passo 5.

## Limpeza

```powershell
az group delete -n rg-agente-viagens --yes
```
