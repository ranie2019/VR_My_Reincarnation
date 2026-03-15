using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Toca um único vídeo ou reproduz a partir de uma lista de vídeos 
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class PlayVideo : MonoBehaviour
{
    [Tooltip("Se o vídeo deve tocar ao iniciar")]
    public bool playAtStart = false;

    [Tooltip("Material usado para reproduzir o vídeo (Usa URP/Unlit por padrão)")]
    public Material videoMaterial = null;

    [Tooltip("Lista de clipes de vídeo para escolher")]
    public List<VideoClip> videoClips = new List<VideoClip>();

    private VideoPlayer videoPlayer = null;
    private MeshRenderer meshRenderer = null;

    private int index = 0;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        videoPlayer = GetComponent<VideoPlayer>();

        if (videoClips.Count > 0)
            videoPlayer.clip = videoClips[0];
    }

    private void OnEnable()
    {
        videoPlayer.prepareCompleted += ApplyVideoMaterial;
    }

    private void OnDisable()
    {
        videoPlayer.prepareCompleted -= ApplyVideoMaterial;
    }

    private void Start()
    {
        if (playAtStart)
        {
            Play();
        }
        else
        {
            Stop();
        }
    }

    public void NextClip()
    {
        index = ++index % videoClips.Count;
        Play();
    }

    public void PreviousClip()
    {
        index = --index % videoClips.Count;
        Play();
    }

    public void RandomClip()
    {
        if (videoClips.Count > 0)
        {
            index = Random.Range(0, videoClips.Count);
            Play();
        }
    }

    public void PlayAtIndex(int value)
    {
        if (videoClips.Count > 0)
        {
            index = Mathf.Clamp(value, 0, videoClips.Count);
            Play();
        }
    }

    public void Play()
    {
        videoMaterial.color = Color.white;
        videoPlayer.Play();
    }

    public void Stop()
    {
        videoMaterial.color = Color.black;
        videoPlayer.Stop();
    }

    public void TogglePlayStop()
    {
        bool isPlaying = !videoPlayer.isPlaying;
        SetPlay(isPlaying);
    }

    public void TogglePlayPause()
    {
        if (videoPlayer.isPlaying)
            videoPlayer.Pause();
        else
            Play();
    }

    public void SetPlay(bool value)
    {
        if (value)
        {
            Play();
        }
        else
        {
            Stop();
        }
    }

    private void ApplyVideoMaterial(VideoPlayer source)
    {
        meshRenderer.material = videoMaterial;
    }

    private void OnValidate()
    {
        // Verifica se o material do vídeo é nulo
        if (videoMaterial == null)
        {
            // Tenta encontrar o shader "Universal Render Pipeline/Unlit"
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                // Cria um novo material com o shader encontrado
                videoMaterial = new Material(shader);
            }
            else
            {
                // Registra um erro no console se o shader não for encontrado
                Debug.LogError("Shader 'Universal Render Pipeline/Unlit' não encontrado! Certifique-se de que o shader está incluído no projeto.");
            }
        }
    }
}
