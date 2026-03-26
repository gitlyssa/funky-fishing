studio.menu.addMenuItem({ 
    name: "Export Visual Events",
    execute: function() {
        var event = studio.window.browserCurrent();
        if (!event || !event.isOfType("Event")) {
            alert("Please select an Event first.");
            return;
        }

        var markers = event.markerTrack.markers;
        var output = "Timestamp,Command,Params\n";
        var validPrefixes = ["Profile:", "Pulse:", "Flash:", "Ripple:"];

        markers.forEach(function(marker) {
            if (marker.isOfType("NamedMarker")) {
                var name = marker.name;
                
                // Check for the colon separator
                if (name.indexOf(':') === -1) return;

                var prefix = name.split(':')[0] + ":";
                
                if (validPrefixes.indexOf(prefix) !== -1) {
                    var timestamp = marker.position / 1000; // Convert ms to seconds
                    var params = name.split(':')[1].trim();
                    
                    output += timestamp.toFixed(3) + "," + prefix.replace(":", "") + "," + params + "\n";
                }
            }
        });

        var path = studio.project.filePath.split("/").slice(0, -1).join("/") + "/Visual_Events.txt";
        var file = studio.system.getFile(path);
        file.open(studio.system.openMode.WriteOnly);
        file.writeText(output);
        file.close();

        alert("Visual Events exported to: " + path);
    }
});