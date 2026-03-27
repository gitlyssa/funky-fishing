
///Profile: <profile_name> <transition_duration>
///Pulse: <intensity> <groups> ...
///Flash: <hexColor> <intensity> <duration>
//Ripple: <x><y><z> <speed> <intensity> <duration>
//Blackout: <bool> <duration>
//FireflyAgitation: <speed> <range> <duration>
///BloomKick: <intensity> <duration>
///Glitch: <intensity> <duration>

function getProjectDir() {
    var p = studio.project.filePath;
    return p.substr(0, p.lastIndexOf("/") + 1);
}

studio.menu.addMenuItem({
    name: "Export/Visual Events (Timestamp,Command,Params)",
    isEnabled: function () {
        var ev = studio.window.browserCurrent();
        return ev && ev.isOfExactType("Event");
    },
    execute: function () {
        var ev = studio.window.browserCurrent();
        if (!ev) {
            alert("No event selected.");
            return;
        }

        // 1. Get BPM from the first Tempo Marker
        var bpm = "Unknown";
        for (var t = 0; t < ev.markerTracks.length; t++) {
            var track = ev.markerTracks[t];
            var markers = track.markers.slice();
            for (var i = 0; i < markers.length; i++) {
                if (markers[i].isOfExactType("TempoMarker")) {
                    bpm = markers[i].tempo;
                    break;
                }
            }
            if (bpm !== "Unknown") break;
        }

        // 2. Sanitize filename based on Event Name
        var safeName = ev.name.replace(/[\\/:*?"<>|]/g, "_");
        var outPath = getProjectDir() + safeName + "_Visual_Events.txt";
        
        var file = studio.system.getFile(outPath);
        if (!file.open(studio.system.openMode.WriteOnly)) {
            alert("Failed to open file:\n" + outPath);
            return;
        }

        // 3. Write Metadata and Header
        file.writeText("BPM: " + bpm + "\n");
        file.writeText("Timestamp,Command,Params\n");

        var validPrefixes = [
            "Profile", "Pulse", "Flash", "Ripple", 
            "Blackout", "FireflyAgitation", "BloomKick", "Glitch", "FireflyPulse"
        ];

        // Collect all markers from all tracks first
        var allMarkers = [];
        for (var t = 0; t < ev.markerTracks.length; t++) {
            var track = ev.markerTracks[t];
            var trackMarkers = track.markers.slice(); // Convert to real JS array
            for (var i = 0; i < trackMarkers.length; i++) {
                allMarkers.push(trackMarkers[i]);
            }
        }

        // Sort chronologically so Unity doesn't have to
        allMarkers.sort(function (a, b) { return a.position - b.position; });

        for (var j = 0; j < allMarkers.length; j++) {
            var m = allMarkers[j];

            if (m.isOfExactType("NamedMarker")) {
                var name = m.name;
                
                // Ensure it has the colon separator we need
                if (name.indexOf(':') !== -1) {
                    var parts = name.split(':');
                    var cmd = parts[0].trim();
                    var params = parts[1].trim();

                    // Check if the command is in our valid list
                    var isValid = false;
                    for (var k = 0; k < validPrefixes.length; k++) {
                        if (cmd === validPrefixes[k]) {
                            isValid = true;
                            break;
                        }
                    }

                    if (isValid) {
                        file.writeText(m.position + "," + cmd + "," + params + "\n");
                    }
                }
            }
        }

        file.close();
        alert("Visual Events exported successfully to:\n" + outPath);
    }
});