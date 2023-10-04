# MQTT Client for local Tuya devices
## project scope & intended use
<img src="doc/logo.png" 
     style="width:80px;" />
This is a .NET7.0 Blazor application to implement a server which is a MQTT client

- it polls local tuya devices in the network
   - publishes it data points
- it polls Cloud tuya devices
   - publishes it data points
- subscribes to commands to allow setting datapoints in the tuya device (locally and in the cloud)

I use this server as wrapper between my socket switches and my [IP-SYMCON](https://www.symcon.de/) instance.
The existing impmentations, I found either did not work for me or did not provide the needed functions, I was looking for. 
So creating a solution myself to get the functions to work has been the motivation for this project.

Still also any other home automation environment which can deal with MQTT shall work completely.

## supported devices
In general all TUYA devices shall be supported. 
The fundamental intention has been using the devices through the localTuya library.
Battery driven sensors are not supported as those do not offer a local port. To use those devices in the same way, there is a implementation to poll those data points over the cloud access.

So Tuya cloud AccessKey is nice, but not a must.

## used other github projects
implements a MQTT Client (based on [MQTTnet](https://github.com/dotnet/MQTTnet))
implements a local Tuya communicator (based on [tuyanet](https://github.com/ClusterM/tuyanet))

## how to use
follow the [WIKI](https://github.com/volker76/tuya_mqtt.net/wiki) instructions. 
