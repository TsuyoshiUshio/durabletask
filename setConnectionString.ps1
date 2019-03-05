$jobname = [Environment]::GetEnvironmentVariable("AGENT_JOBNAME")
$jobid = $jobname.Substring($jobname.Length -1, 1)

switch ($jobid) {
  "1" {  [Environment]::SetEnvironmentVariable("DurableTaskTestServiceBusConnectionString", "$(DurableTaskTestServiceBusConnectionString1)");
            break}
  "2" {  [Environment]::SetEnvironmentVariable("DurableTaskTestServiceBusConnectionString", "$(DurableTaskTestServiceBusConnectionString2)");
            break}  
  "3" {  [Environment]::SetEnvironmentVariable("DurableTaskTestServiceBusConnectionString", "$(DurableTaskTestServiceBusConnectionString3)");
            break}  
  "4" {  [Environment]::SetEnvironmentVariable("DurableTaskTestServiceBusConnectionString", "$(DurableTaskTestServiceBusConnectionString4)");
            break}  
  "5" {  [Environment]::SetEnvironmentVariable("DurableTaskTestServiceBusConnectionString", "$(DurableTaskTestServiceBusConnectionString5)");
            break}  
  default 
     { Write-Host "$($jobid)"; break; }
}
