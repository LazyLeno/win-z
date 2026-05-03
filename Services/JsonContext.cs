using System.Text.Json.Serialization;
using WinZ.Models;
using WinZ.Engine;
using System.Collections.Generic;

namespace WinZ.Services;

[JsonSerializable(typeof(List<SetupTask>))]
[JsonSerializable(typeof(SetupTask))]
[JsonSerializable(typeof(SetupResult))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
