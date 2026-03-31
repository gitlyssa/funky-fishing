//something here?
studio.menu.addMenuItem({
    name: "Clean Auto-Appended Letters",
    isEnabled: function () {
        var items = studio.window.editorSelection();
        return items.length > 0;
    },
    execute: function () {
        var items = studio.window.editorSelection();
        
        var regex = /\s[a-zA-Z]$/;

        // studio.project.workspace.build();

        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            
            if (item.isOfType("Marker") && item.name) {
                
                // If the name matches the " space + letter at the end" rule
                if (regex.test(item.name)) {
                    // Replace that specific chunk with nothing ("")
                    item.name = item.name.replace(regex, "");
                }
            }
        }
    }
});