# MQTT Client for local Tuya devices
## project scope & intended use
This is a .NET7.0 Blazor application to implement a server which is a MQTT client
- it polls local tuya devices in the network
   - publishes it data points
- subscribes to commands to allow setting datapoints in the tuya device

I use this server as wrapper between my socket switches and my [IP-SYMCON](https://www.symcon.de/) instance.
The existing impmentations either did not work for me or did not provide the needed functions, I was looking for. 


## supported devices
In general all TUYA devices shall be supported which can be access via localTuya.

Battery driven sensors are noit supported as those do not offer a local port.

## used other github projects
implements a MQTT Client (based on [MQTTnet](https://github.com/dotnet/MQTTnet))

implements a local Tuya communicator (based on [tuyanet](https://github.com/ClusterM/tuyanet))

Thanks to all the contributors of those projects to set the foundation for the TuyaMQtt.net client