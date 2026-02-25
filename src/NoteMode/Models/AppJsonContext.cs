using System.Text.Json.Serialization;

namespace NoteMode.Models;

[JsonSerializable(typeof(AppState))]
[JsonSerializable(typeof(NotesIndex))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppJsonContext : JsonSerializerContext
{
}
