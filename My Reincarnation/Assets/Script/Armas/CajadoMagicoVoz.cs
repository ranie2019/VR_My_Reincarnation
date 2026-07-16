using System;
using System.Collections;
using System.Text;
using Meta.WitAi.Configuration;
using Meta.WitAi.Requests;
using Oculus.Voice;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class CajadoMagicoVoz : MonoBehaviour
{
    public enum EstadoVozCajado
    {
        Desativado,
        SemReferenciaVoiceSDK,
        SemPermissaoMicrofone,
        SolicitandoPermissao,
        Pronto,
        Escutando,
        Processando,
        Erro
    }

    [Serializable]
    public class EventoComandoVoz : UnityEvent<string>
    {
    }

    [Header("Configuracao de Voz")]
    [SerializeField] private AppVoiceExperience appVoiceExperience;
    [SerializeField] private bool escutarAutomaticamenteAoSegurar = true;
    [SerializeField, Min(0f)] private float atrasoParaReativarEscuta = 0.35f;
    [SerializeField] private bool solicitarPermissaoAutomaticamente = true;
    [SerializeField] private bool mostrarDiagnostico;

    [Header("Eventos")]
    [SerializeField] private EventoComandoVoz aoComandoVozRecebido = new EventoComandoVoz();

    [Header("Diagnostico")]
    [SerializeField] private EstadoVozCajado estadoVoz = EstadoVozCajado.Desativado;
    [SerializeField] private bool cajadoSegurado;
    [SerializeField] private bool permissaoMicrofoneConcedida;
    [SerializeField] private bool estaEscutando;
    [SerializeField] private bool estaProcessando;
    [SerializeField] private string ultimaTranscricaoBruta;
    [SerializeField] private string ultimaTranscricaoNormalizada;
    [SerializeField] private float horarioUltimaTranscricao;
    [SerializeField] private string ultimoErro;
    [SerializeField] private Transform ultimoInteractor;

    private XRBaseInteractable interagivelXR;
    private XRBaseInteractable interagivelRegistrado;
    private Coroutine rotinaReativarEscuta;
    private int idSessaoVoz;
    private int idSessaoCancelada;
    private bool cancelamentoAtivo;
    private bool listenersVoiceRegistrados;
    private bool permissaoSolicitadaAutomaticamente;
    private bool avisoSemInteragivelMostrado;
    private bool avisoSemVoiceMostrado;

#if UNITY_ANDROID && !UNITY_EDITOR
    private UnityEngine.Android.PermissionCallbacks callbacksPermissaoMicrofone;
#endif

    public EstadoVozCajado EstadoVoz => estadoVoz;
    public bool CajadoSegurado => cajadoSegurado;
    public bool PermissaoMicrofoneConcedida => permissaoMicrofoneConcedida;
    public bool EstaEscutando => estaEscutando;
    public bool EstaProcessando => estaProcessando;
    public string UltimaTranscricaoBruta => ultimaTranscricaoBruta;
    public string UltimaTranscricaoNormalizada => ultimaTranscricaoNormalizada;
    public EventoComandoVoz AoComandoVozRecebido => aoComandoVozRecebido;

    private void Awake()
    {
        EncontrarAppVoiceExperienceSeNecessario();
        EncontrarInteragivelXRSeNecessario();
        AtualizarPermissaoMicrofone();
        AtualizarEstadoInicial();
    }

    private void OnEnable()
    {
        EncontrarAppVoiceExperienceSeNecessario();
        EncontrarInteragivelXRSeNecessario();
        RegistrarEventosXR();
        RegistrarEventosVoiceSDK();
        AtualizarEstadoSelecao();
        AtualizarPermissaoMicrofone();
        AtualizarEstadoInicial();
    }

    private void OnDisable()
    {
        PararEscutaVoz();
        RemoverEventosXR();
        RemoverEventosVoiceSDK();
        cajadoSegurado = false;
        ultimoInteractor = null;
        estadoVoz = EstadoVozCajado.Desativado;
    }

    private void OnDestroy()
    {
        PararEscutaVoz();
        RemoverEventosXR();
        RemoverEventosVoiceSDK();
    }

    private void OnValidate()
    {
        atrasoParaReativarEscuta = Mathf.Max(0f, atrasoParaReativarEscuta);
        EncontrarAppVoiceExperienceSeNecessario();
        EncontrarInteragivelXRSeNecessario();
        AtualizarPermissaoMicrofone();
    }

    public void IniciarEscutaVoz()
    {
        if (!isActiveAndEnabled)
            return;

        EncontrarAppVoiceExperienceSeNecessario();
        AtualizarEstadoSelecao();

        if (!cajadoSegurado)
        {
            estadoVoz = EstadoVozCajado.Desativado;
            return;
        }

        if (appVoiceExperience == null)
        {
            estadoVoz = EstadoVozCajado.SemReferenciaVoiceSDK;
            AvisarSemVoiceUmaVez();
            return;
        }

        if (!GarantirPermissaoMicrofone())
            return;

        if (estaEscutando || estaProcessando || appVoiceExperience.MicActive || appVoiceExperience.IsRequestActive)
            return;

        string erroAtivacao = appVoiceExperience.GetActivateAudioError();
        string erroEnvio = appVoiceExperience.GetSendError();

        if (!appVoiceExperience.CanActivateAudio() || !appVoiceExperience.CanSend())
        {
            ultimoErro = !string.IsNullOrEmpty(erroAtivacao) ? erroAtivacao : erroEnvio;
            estadoVoz = EstadoVozCajado.Erro;
            return;
        }

        PararRotinaReativacao();
        cancelamentoAtivo = false;

        int sessaoAtual = ++idSessaoVoz;
        VoiceServiceRequestEvents eventosSessao = CriarEventosSessao(sessaoAtual);
        VoiceServiceRequest request = appVoiceExperience.Activate(new WitRequestOptions(), eventosSessao);

        if (request == null)
        {
            ultimoErro = "O AppVoiceExperience nao iniciou uma requisicao de voz.";
            estadoVoz = EstadoVozCajado.Erro;
            estaEscutando = false;
            estaProcessando = false;
            return;
        }

        estaEscutando = true;
        estaProcessando = false;
        estadoVoz = EstadoVozCajado.Escutando;
    }

    public void PararEscutaVoz()
    {
        PararRotinaReativacao();
        cancelamentoAtivo = true;
        idSessaoCancelada = idSessaoVoz;

        if (appVoiceExperience != null && (appVoiceExperience.MicActive || appVoiceExperience.Active || appVoiceExperience.IsRequestActive))
            appVoiceExperience.DeactivateAndAbortRequest();

        estaEscutando = false;
        estaProcessando = false;
        estadoVoz = cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado;
    }

    public void SolicitarPermissaoMicrofone()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            permissaoMicrofoneConcedida = true;
            estadoVoz = cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado;
            return;
        }

        callbacksPermissaoMicrofone = new UnityEngine.Android.PermissionCallbacks();
        callbacksPermissaoMicrofone.PermissionGranted += AoPermissaoMicrofoneConcedida;
        callbacksPermissaoMicrofone.PermissionDenied += AoPermissaoMicrofoneNegada;
        callbacksPermissaoMicrofone.PermissionDeniedAndDontAskAgain += AoPermissaoMicrofoneNegada;

        estadoVoz = EstadoVozCajado.SolicitandoPermissao;
        UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacksPermissaoMicrofone);
#else
        permissaoMicrofoneConcedida = true;
        estadoVoz = cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado;
#endif
    }

    private VoiceServiceRequestEvents CriarEventosSessao(int sessao)
    {
        VoiceServiceRequestEvents eventos = new VoiceServiceRequestEvents();
        eventos.OnStartListening.AddListener(_ => AoIniciarEscuta(sessao));
        eventos.OnStopListening.AddListener(_ => AoPararEscuta(sessao));
        eventos.OnSend.AddListener(_ => AoEnviarAudio(sessao));
        eventos.OnFullTranscription.AddListener(texto => AoReceberTranscricaoCompleta(texto, sessao));
        eventos.OnCancel.AddListener(_ => AoCancelarSessao(sessao));
        eventos.OnFailed.AddListener(_ => AoFalharSessao(sessao));
        eventos.OnComplete.AddListener(_ => AoCompletarSessao(sessao));
        return eventos;
    }

    private void AoIniciarEscuta(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = true;
        estaProcessando = false;
        estadoVoz = EstadoVozCajado.Escutando;
    }

    private void AoPararEscuta(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = true;
        estadoVoz = EstadoVozCajado.Processando;
    }

    private void AoEnviarAudio(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = true;
        estadoVoz = EstadoVozCajado.Processando;
    }

    private void AoReceberTranscricaoCompleta(string texto, int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        ultimaTranscricaoBruta = texto ?? string.Empty;
        ultimaTranscricaoNormalizada = NormalizarTranscricao(ultimaTranscricaoBruta);
        horarioUltimaTranscricao = Time.time;

        if (string.IsNullOrEmpty(ultimaTranscricaoNormalizada))
            return;

        aoComandoVozRecebido?.Invoke(ultimaTranscricaoNormalizada);
    }

    private void AoCancelarSessao(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = false;
        estadoVoz = cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado;
    }

    private void AoFalharSessao(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = false;
        ultimoErro = "Falha ao processar a escuta de voz.";
        estadoVoz = EstadoVozCajado.Erro;
        AgendarReativacaoAutomatica();
    }

    private void AoCompletarSessao(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = false;
        estadoVoz = cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado;
        AgendarReativacaoAutomatica();
    }

    private void AoErroVoiceSDK(string tipoErro, string mensagem)
    {
        ultimoErro = string.IsNullOrEmpty(tipoErro) ? mensagem : $"{tipoErro}: {mensagem}";
        estaEscutando = false;
        estaProcessando = false;
        estadoVoz = EstadoVozCajado.Erro;
    }

    private bool SessaoAtualValida(int sessao)
    {
        return isActiveAndEnabled
            && cajadoSegurado
            && !cancelamentoAtivo
            && sessao == idSessaoVoz
            && sessao != idSessaoCancelada;
    }

    private void AgendarReativacaoAutomatica()
    {
        if (!escutarAutomaticamenteAoSegurar || !cajadoSegurado || !isActiveAndEnabled)
            return;

        if (rotinaReativarEscuta != null)
            return;

        rotinaReativarEscuta = StartCoroutine(ReativarEscutaDepoisDoAtraso());
    }

    private IEnumerator ReativarEscutaDepoisDoAtraso()
    {
        if (atrasoParaReativarEscuta > 0f)
            yield return new WaitForSeconds(atrasoParaReativarEscuta);

        rotinaReativarEscuta = null;

        if (escutarAutomaticamenteAoSegurar && cajadoSegurado && isActiveAndEnabled)
            IniciarEscutaVoz();
    }

    private void PararRotinaReativacao()
    {
        if (rotinaReativarEscuta == null)
            return;

        StopCoroutine(rotinaReativarEscuta);
        rotinaReativarEscuta = null;
    }

    private bool GarantirPermissaoMicrofone()
    {
        AtualizarPermissaoMicrofone();

        if (permissaoMicrofoneConcedida)
            return true;

        estadoVoz = EstadoVozCajado.SemPermissaoMicrofone;

        if (solicitarPermissaoAutomaticamente && !permissaoSolicitadaAutomaticamente)
        {
            permissaoSolicitadaAutomaticamente = true;
            SolicitarPermissaoMicrofone();
        }

        return false;
    }

    private void AtualizarPermissaoMicrofone()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        permissaoMicrofoneConcedida = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
        permissaoMicrofoneConcedida = true;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void AoPermissaoMicrofoneConcedida(string permissao)
    {
        if (permissao != UnityEngine.Android.Permission.Microphone)
            return;

        permissaoMicrofoneConcedida = true;
        estadoVoz = cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado;

        if (escutarAutomaticamenteAoSegurar && cajadoSegurado && isActiveAndEnabled)
            IniciarEscutaVoz();
    }

    private void AoPermissaoMicrofoneNegada(string permissao)
    {
        if (permissao != UnityEngine.Android.Permission.Microphone)
            return;

        permissaoMicrofoneConcedida = false;
        ultimoErro = "Permissao de microfone negada.";
        estadoVoz = EstadoVozCajado.SemPermissaoMicrofone;
    }
#endif

    private void RegistrarEventosXR()
    {
        if (interagivelRegistrado != null)
            return;

        if (interagivelXR == null)
        {
            AvisarSemInteragivelUmaVez();
            return;
        }

        interagivelXR.selectEntered.AddListener(AoSelecionarCajado);
        interagivelXR.selectExited.AddListener(AoSoltarCajado);
        interagivelRegistrado = interagivelXR;
    }

    private void RemoverEventosXR()
    {
        if (interagivelRegistrado == null)
            return;

        interagivelRegistrado.selectEntered.RemoveListener(AoSelecionarCajado);
        interagivelRegistrado.selectExited.RemoveListener(AoSoltarCajado);
        interagivelRegistrado = null;
    }

    private void RegistrarEventosVoiceSDK()
    {
        if (listenersVoiceRegistrados || appVoiceExperience == null)
            return;

        appVoiceExperience.VoiceEvents.OnError.AddListener(AoErroVoiceSDK);
        listenersVoiceRegistrados = true;
    }

    private void RemoverEventosVoiceSDK()
    {
        if (!listenersVoiceRegistrados || appVoiceExperience == null)
            return;

        appVoiceExperience.VoiceEvents.OnError.RemoveListener(AoErroVoiceSDK);
        listenersVoiceRegistrados = false;
    }

    private void AoSelecionarCajado(SelectEnterEventArgs args)
    {
        cajadoSegurado = true;
        ultimoInteractor = ObterTransformInteractor(args != null ? args.interactorObject : null);
        cancelamentoAtivo = false;
        idSessaoCancelada = -1;
        estadoVoz = appVoiceExperience != null ? EstadoVozCajado.Pronto : EstadoVozCajado.SemReferenciaVoiceSDK;

        if (escutarAutomaticamenteAoSegurar)
            IniciarEscutaVoz();
    }

    private void AoSoltarCajado(SelectExitEventArgs args)
    {
        cajadoSegurado = false;
        ultimoInteractor = null;
        PararEscutaVoz();
    }

    private void AtualizarEstadoSelecao()
    {
        if (interagivelXR == null)
        {
            cajadoSegurado = false;
            ultimoInteractor = null;
            return;
        }

        cajadoSegurado = interagivelXR.isSelected;

        if (cajadoSegurado)
        {
            if (ultimoInteractor == null)
                ultimoInteractor = ObterPrimeiroInteractorSelecionando();
        }
        else
        {
            ultimoInteractor = null;
        }
    }

    private void AtualizarEstadoInicial()
    {
        if (appVoiceExperience == null)
        {
            estadoVoz = EstadoVozCajado.SemReferenciaVoiceSDK;
            return;
        }

        if (!permissaoMicrofoneConcedida)
        {
            estadoVoz = EstadoVozCajado.SemPermissaoMicrofone;
            return;
        }

        estadoVoz = cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado;
    }

    private void EncontrarAppVoiceExperienceSeNecessario()
    {
        if (appVoiceExperience != null)
            return;

        appVoiceExperience = FindFirstObjectByType<AppVoiceExperience>();
    }

    private void EncontrarInteragivelXRSeNecessario()
    {
        if (interagivelXR != null)
            return;

        interagivelXR = GetComponent<XRBaseInteractable>();

        if (interagivelXR == null)
            interagivelXR = GetComponentInParent<XRBaseInteractable>();

        if (interagivelXR == null)
            interagivelXR = GetComponentInChildren<XRBaseInteractable>(true);
    }

    private Transform ObterPrimeiroInteractorSelecionando()
    {
        if (interagivelXR == null || interagivelXR.interactorsSelecting == null || interagivelXR.interactorsSelecting.Count == 0)
            return null;

        return ObterTransformInteractor(interagivelXR.interactorsSelecting[0]);
    }

    private static Transform ObterTransformInteractor(IXRInteractor interactor)
    {
        return interactor is Component componente ? componente.transform : null;
    }

    private static string NormalizarTranscricao(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        string textoLimpo = texto.Trim().ToLowerInvariant();
        StringBuilder builder = new StringBuilder(textoLimpo.Length);
        bool espacoAnterior = false;

        for (int i = 0; i < textoLimpo.Length; i++)
        {
            char caractere = textoLimpo[i];

            if (char.IsWhiteSpace(caractere))
            {
                if (!espacoAnterior)
                {
                    builder.Append(' ');
                    espacoAnterior = true;
                }

                continue;
            }

            builder.Append(caractere);
            espacoAnterior = false;
        }

        return builder.ToString();
    }

    private void AvisarSemInteragivelUmaVez()
    {
        if (avisoSemInteragivelMostrado)
            return;

        avisoSemInteragivelMostrado = true;

        if (mostrarDiagnostico)
            Debug.LogWarning("CajadoMagicoVoz precisa de um XRBaseInteractable ou XRGrabInteractable no cajado.", this);
    }

    private void AvisarSemVoiceUmaVez()
    {
        if (avisoSemVoiceMostrado)
            return;

        avisoSemVoiceMostrado = true;

        if (mostrarDiagnostico)
            Debug.LogWarning("CajadoMagicoVoz nao encontrou um AppVoiceExperience na cena.", this);
    }
}
