There are two solutions. The Hexapi UI Service host, is a UWP app that does nothing other than host the hexapi service.
I am getting better performance doing it this way rather than using a background task.

The other solution is the HexaPI Remote UI. It is also a UWP app that offers viewing of streaming video over MQTT.
The xBox controller works through this app, the movements of the controller are sampled at a adjustable rate and published to the hexapod.
