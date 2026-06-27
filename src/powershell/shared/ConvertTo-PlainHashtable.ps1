function ConvertTo-PlainHashtable {
    param([object]$InputObject)
    $result = @{}
    if ($null -eq $InputObject) { return $result }
    if ($InputObject -is [hashtable]) {
        foreach ($key in $InputObject.Keys) { $result[[string]$key] = $InputObject[$key] }
        return $result
    }
    foreach ($property in $InputObject.PSObject.Properties) {
        if ($property.Value -is [System.Collections.IEnumerable] -and $property.Value -isnot [string]) {
            $result[$property.Name] = @($property.Value)
        } else {
            $result[$property.Name] = $property.Value
        }
    }
    return $result
}
