using MoonWorks.Audio;

namespace MyGame.Audio;

public class AudioManager
{
    [CVar("volume", "Sets the volume (0 - 1f)")]
    public static float Volume = 0.01f;

    private Dictionary<string, StaticSound> _staticSound = new();
    private Dictionary<string, List<StaticSoundInstance>> _instances = new();

    public AudioManager(MyGameMain game)
    {
        var soundEffects = typeof(ContentPaths.sfx).GetFields()
            .Select(f => f.GetRawConstantValue())
            .Cast<string>()
            .ToArray();

        for (var i = 0; i < soundEffects.Length; i++)
        {
            var wav = StaticSound.LoadWav(game.AudioDevice, soundEffects[i]);
            _staticSound.Add(soundEffects[i], wav);
        }
    }

    public void Play(string path)
    {
        var soundInstance = _staticSound[path].GetInstance();
        if (!_instances.ContainsKey(path))
            _instances[path] = new List<StaticSoundInstance>();
        _instances[path].Add(soundInstance);
        soundInstance.Volume = Volume;
        soundInstance.Play();
    }

    public void Update(float deltaSeconds)
    {
        foreach (var (path, instances) in _instances)
        {
            for (var i = instances.Count - 1; i >= 0; i--)
            {
                var soundInstance = instances[i];
                if (soundInstance.State == SoundState.Stopped)
                {
                    soundInstance.Dispose();
                    instances.RemoveAt(i);
                }
                else
                {
                    soundInstance.Volume = Volume;
                }
            }
        }
    }
}
