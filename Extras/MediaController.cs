using System.Text.RegularExpressions;
using WlxOverlay.GUI;
using WlxOverlay.GFX;

namespace WlxOverlay.Extras;

public class MediaController : IDisposable
{

    private static MediaController _instance = null!;
    public static MediaController Instance => _instance;
    public String mediaPlayerName = "spotify";

    public bool isPlaying = false;
    
    public Dictionary<string, string> metadata = new Dictionary<string, string>();
    public string title = "";
    public string artist = "";
    public string album = "";
    public int positionSeconds = 0;
    public int durationSeconds = 0;
    public float positionPercent = 0;

    public Dictionary<string, Button> buttons = new Dictionary<string, Button>();
    public Dictionary<string, Label> labels = new Dictionary<string, Label>();

    public Timer updateTimer = null!;
    public static Dictionary<string, string> commands = new Dictionary<string, string> {
        {
            "playPause", "dbus-send --print-reply --dest=org.mpris.MediaPlayer2.{} /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player.PlayPause"
        },
        {
            "next", "dbus-send --print-reply --dest=org.mpris.MediaPlayer2.{} /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player.Next"
        },
        {
            "previous", "dbus-send --print-reply --dest=org.mpris.MediaPlayer2.{} /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player.Previous"
        },
        {
            "stop", "dbus-send --print-reply --dest=org.mpris.MediaPlayer2.{} /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player.Stop"
        },
        {
            "playbackStatus", "dbus-send --print-reply --dest=org.mpris.MediaPlayer2.{} /org/mpris/MediaPlayer2 org.freedesktop.DBus.Properties.Get string:org.mpris.MediaPlayer2.Player string:PlaybackStatus"
        },
        {
            "metadata", "dbus-send --print-reply --dest=org.mpris.MediaPlayer2.{} /org/mpris/MediaPlayer2 org.freedesktop.DBus.Properties.Get string:org.mpris.MediaPlayer2.Player string:Metadata"
        },
        {
            "position", "dbus-send --print-reply --dest=org.mpris.MediaPlayer2.{} /org/mpris/MediaPlayer2 org.freedesktop.DBus.Properties.Get string:org.mpris.MediaPlayer2.Player string:Position"
        }
    };

    public static void Initialize()
    {
        _instance = new MediaController();
        _instance.Start();
    }

    private void Start()
    {
        updateTimer = new Timer(_ => {
            _ = MediaController.Instance.statusUpdate();
        }, null, 0, 1000);
    }

    private TimerCallback statusUpdate() {

        var playbackStatusResponse = RunCommand(BuildCommand("playbackStatus", mediaPlayerName));
        var metadataResponse = RunCommand(BuildCommand("metadata", mediaPlayerName));
        var positionResponse = RunCommand(BuildCommand("position", mediaPlayerName));

        this.metadata = ParseDbusResponse(metadataResponse);
        isPlaying = playbackStatusResponse.Contains("Playing");

        Regex positionRegex = new(@"int64 (?<position>\d+)");

        var position = int.Parse(positionRegex.Match(positionResponse).Groups["position"].Value) / 1000000;
        var duration = int.Parse(this.metadata["mpris:length"]) / 1000000;

        var positionPercent = (float)position / (float)duration;

        if (this.metadata.ContainsKey("xesam:title")) {
            title = this.metadata["xesam:title"];
        } else {
            title = "";
        }

        if (this.metadata.ContainsKey("xesam:artist")) {
            artist = this.metadata["xesam:artist"];
        } else {
            artist = "";
        }

        if (this.metadata.ContainsKey("xesam:album")) {
            album = this.metadata["xesam:album"];
        } else {
            album = "";
        }

        this.positionSeconds = position;
        this.durationSeconds = duration;
        this.positionPercent = positionPercent;
        
        if (buttons.ContainsKey("playPause")) {
            buttons["playPause"].SetText(isPlaying ? "⏸" : "▶️");
        }

        if (labels.ContainsKey("song")) {
            labels["song"].Text = title + " - " + artist;
            labels["song"].Update();
        }

        if (labels.ContainsKey("time")) {
            labels["time"].Text = FormatSeconds(position) + " / " + FormatSeconds(duration);
            labels["time"].Update();
        }
        
        updateTimer.Change(1000, 1000);
        return null!;

    }

    static string[] BuildCommand(string commandName, string player)
    {
        return commands[commandName].Split(' ').Select(s => s.Replace("{}", player)).ToArray();
    }


    static string RunCommand(string[] command)
    {
        var procStartInfo = new ProcessStartInfo(command[0], string.Join(" ", command.Skip(1)))
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var proc = new Process { StartInfo = procStartInfo };
        proc.Start();
        return proc.StandardOutput.ReadToEnd();
    }

    public static Dictionary<string, string> ParseDbusResponse(string res) {
        Regex regex = new(@"dict entry\(\n.*?""(?<key>.+?)""\n +variant +(?>(?>array \[\n +string ""?(?<value>.*?)""?\n)|(?>.*? ""?(?<value>.*?)""?\n))");

        var matches = regex.Matches(res);
        var dict = new Dictionary<string, string>();
        foreach (Match match in matches.Cast<Match>())
        {
            dict.Add(match.Groups["key"].Value, match.Groups["value"].Value);
            // Console.WriteLine(match.Groups["key"].Value + " " + match.Groups["value"].Value);
        }

        return dict;
    } 

    public void playPause()
    {
       RunCommand(BuildCommand("playPause", mediaPlayerName));
       isPlaying = !isPlaying;
       buttons["playPause"].SetText(isPlaying ? "⏸" : "▶️");
    }

    public void next()
    {
        RunCommand(BuildCommand("next", mediaPlayerName));
    }

    public void previous()
    {
        RunCommand(BuildCommand("previous", mediaPlayerName));
    }

    public void stop()
    {
        RunCommand(BuildCommand("stop", mediaPlayerName));
    }

    public void AddControls(Canvas canvas) {

        buttons.Clear();
        labels.Clear();

        buttons.Add("previous", new Button("⏮", 2, 186, 46, 32){PointerDown = _ => MediaController.Instance.previous()});
        buttons.Add("playPause", new Button("⏯", 46, 186, 46, 32){PointerDown = _ => MediaController.Instance.playPause()});
        buttons.Add("next", new Button("⏭", 90, 186, 46, 32) {PointerDown = _ => MediaController.Instance.next()});
        buttons.Add("stop", new Button("⏹", 134, 186, 46, 32) {PointerDown = _ => MediaController.Instance.stop()});

        foreach (var button in buttons.Values)
        {
          canvas.AddControl(button);
        }

        Canvas.CurrentFont = FontCollection.Get(14, FontStyle.Bold);
        Canvas.CurrentFgColor = HexColor.FromRgb("#FFFFFF");

        labels.Add("song", new Label("", 0, 162, 400, 32));
        labels.Add("time", new Label("", 180, 194, 46, 32));
      
        foreach (var label in labels.Values)
        {
          canvas.AddControl(label);
        }        
    }

    public static string FormatSeconds(int seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        if (t.Hours > 0)
        {
            return t.ToString(@"hh\:mm\:ss");
        }
        else
        {
            return t.ToString(@"mm\:ss");
        }
    }

    public void Dispose()
    {
        updateTimer.Dispose();
    }
}
