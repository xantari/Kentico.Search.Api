[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri https://localdevinternalapi.yourkenticosite.org/KenticoSearch/api/Search/PopulateAllIndexes?searchServiceName=yourkenticosite.org%20Search `
        -Method POST `
        -Headers @{"accept" = "application/json"}