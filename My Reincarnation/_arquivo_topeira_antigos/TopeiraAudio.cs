using UnityEngine;

// =====================================================================
// TopeiraAudio
// Todos os sons da Topeira. Nenhuma logica de combate ou movimento aqui.
// =====================================================================
[DisallowMultipleComponent]
public class TopeiraAudio : MonoBehaviour
{
    #region Configuracao
    [Tooltip("Se vazio, usa o AudioSource do objeto (ou cria um 3D).")]
    [SerializeField] private AudioSource audioSource;

    [Header("Clipes")]
    [SerializeField] private AudioClip somAndar;
    [SerializeField] private AudioClip somAtacar;
    [SerializeField] private AudioClip somDano;
    [SerializeField] private AudioClip somMorrer;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float volumeAndar = 1f;
    [SerializeField, Range(0f, 1f)] private float volumeAtacar = 1f;
    [SerializeField, Range(0f, 1f)] private float volumeDano = 1f;
    [SerializeField, Range(0f, 1f)] private float volumeMorrer = 1f;
    #endregion

    #region Ciclo de vida
    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
        }
        audioSource.playOnAwake = false;
    }

    private void OnDisable() => PararAndar();
    #endregion

    #region Passos (som em loop)
    public void IniciarAndar()
    {
        if (somAndar == null || audioSource == null) return;
        if (audioSource.isPlaying && audioSource.clip == somAndar) return;
        audioSource.clip = somAndar;
        audioSource.volume = volumeAndar;
        audioSource.loop = true;
        audioSource.Play();
    }

    public void PararAndar()
    {
        if (audioSource != null && audioSource.loop && audioSource.clip == somAndar && audioSource.isPlaying)
            audioSource.Stop();
    }
    #endregion

    #region Sons pontuais
    public void TocarAtaque() => TocarUmaVez(somAtacar, volumeAtacar);
    public void TocarDano() => TocarUmaVez(somDano, volumeDano);

    // O objeto e destruido logo depois: o som de morte toca no ponto, fora do objeto.
    public void TocarMorte()
    {
        if (somMorrer != null) AudioSource.PlayClipAtPoint(somMorrer, transform.position, volumeMorrer);
    }

    private void TocarUmaVez(AudioClip clip, float volume)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume);
    }
    #endregion
}
