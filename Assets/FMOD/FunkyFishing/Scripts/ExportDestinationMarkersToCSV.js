// ExportBeatmapCSV.js
// Exports destination markers as: hitTime,type,direction
// Output: <project directory>/<EventName>_beatmap.csv

function getProjectDir() {
    var p = studio.project.filePath;
    return p.substr(0, p.lastIndexOf("/") + 1);
}

studio.menu.addMenuItem({
    name: "Export/Beatmap CSV (hitTime,type,direction)",
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

        var safeName = ev.name.replace(/[\\\/:*?"<>|]/g, "_");
        var outPath = getProjectDir() + safeName + "_beatmap.csv";

        var file = studio.system.getFile(outPath);
        if (!file.open(studio.system.openMode.WriteOnly)) {
            alert("Failed to open file:\n" + outPath);
            return;
        }

        // Write header
        file.writeText("hitTime,type,direction\n");

        for (var t = 0; t < ev.markerTracks.length; t++) {
            var track = ev.markerTracks[t];

            var markers = track.markers.slice();
            markers.sort(function (a, b) { return a.position - b.position; });

            for (var i = 0; i < markers.length; i++) {
                var m = markers[i];

                if (m.isOfExactType("NamedMarker")) {

                    var direction = m.name.toLowerCase();
                    var type = "tap"; // default for now

                    // Only export valid directions
                    if (direction === "left" ||
                        direction === "up" ||
                        direction === "right") {

                        file.writeText(
                            m.position + "," + type + "," + direction + "\n"
                        );
                    }
                }
            }
        }

        file.close();
        console.log("Beatmap exported to: " + outPath);
    }
});