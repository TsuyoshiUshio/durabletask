Param(
[string]$DurableTaskTestServiceBusConnectionString1,
[string]$DurableTaskTestServiceBusConnectionString2,
[string]$DurableTaskTestServiceBusConnectionString3,
[string]$DurableTaskTestServiceBusConnectionString4,
[string]$DurableTaskTestServiceBusConnectionString5
)

$jobname = [Environment]::GetEnvironmentVariable("AGENT_JOBNAME")
$jobid = $jobname.Substring($jobname.Length -1, 1)

switch ($jobid) {
  "1" {  [Environment]::SetEnvironmentVariable("DurableTaskTestServiceBusConnectionString", "$DurableTaskTestServiceBusConnectionString1", "User");
            break}
  "2" {  [Environment]::SetEnvironmentVariable("DurableTaskTestServiceBusConnectionString", "$DurableTaskTestServiceBusConnectionString2", "User");
            break}  
  "3" {  [Environment]::SetEnvironmentVariable("DurableTaskTestServiceBusConnectionString", "$DurableTaskTestServiceBusConnectionString3", "User");
            break}  
  "4" {  [Environment]::SetEnvironmentVariable("DurableTaskTestServiceBusConnectionString", "$DurableTaskTestServiceBusConnectionString4", "User");
            break}  
  "5" {  [Environment]::SetEnvironmentVariable("DurableTaskTestServiceBusConnectionString", "$DurableTaskTestServiceBusConnectionString5", "User");
            break}  
  default 
     { Write-Host "$($jobid)"; break; }
}
