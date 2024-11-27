using System.Collections.Generic;
using System.Linq;
using NativeFileDialogNET;

namespace CBRE.Editor.Settings;

public static class FileTypeRegistration {
    public static IReadOnlyList<FileType> SupportedExtensions { get; } = [
        new("vmf", "Valve Map Format", true, true),
        new("rmf", "Worldcraft RMF", true, true),
        new("map", "Quake MAP Format", true, true),
        new("3dw", "Leadwerks 3D World Studio File", false, true),

        new("rmx", "Worldcraft RMF (Hammer Backup)", false, true),
        new("max", "Quake MAP Format (Hammer Backup)", false, true),
        new("vmx", "Valve Map Format (Hammer Backup)", false, true),
    ];

    public static NativeFileDialog AddSupportedFiltersSave(this NativeFileDialog diag) {
        foreach (var ext in SupportedExtensions
                     .Where(x => x.CanSave)) {
            diag.AddFilter(ext.Description, ext.Extension);
        }
        return diag;
    }

    public static NativeFileDialog AddSupportedFiltersLoad(this NativeFileDialog diag) {
        diag.AddFilter("All supported formats",
            string.Join(',', SupportedExtensions
                .Where(x => x.CanLoad)
                .Select(x => x.Extension)));
        foreach (var ext in SupportedExtensions
                     .Where(x => x.CanLoad)) {
            diag.AddFilter(ext.Description, ext.Extension);
        }
        return diag;
    }
}
