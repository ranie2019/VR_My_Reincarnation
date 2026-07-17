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
        SemWitConfiguration,
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

    [Header("Eventos")]
    [SerializeField] private EventoComandoVoz aoComandoVozRecebido = new EventoComandoVoz();
    [SerializeField] private EventoComandoVoz aoTextoParcialVozRecebido = new EventoComandoVoz();

    [Header("Estado")]
    [SerializeField] private EstadoVozCajado estadoVoz = EstadoVozCajado.Desativado;
    [SerializeField] private bool cajadoSegurado;
    [SerializeField] private bool permissaoMicrofoneConcedida;
    [SerializeField] private bool estaEscutando;
    [SerializeField] private bool estaProcessando;
    [SerializeField] private string ultimaTranscricaoBruta;
    [SerializeField] private string ultimaTranscricaoNormalizada;
    [SerializeField] private Transform ultimoInteractor;

    private XRBaseInteractable interagivelXR;
    private XRBaseInteractable interagivelRegistrado;
    private Coroutine rotinaReativarEscuta;
    private int idSessaoVoz;
    private int idSessaoCancelada = -1;
    private bool cancelamentoAtivo;
    private bool listenersVoiceRegistrados;
    private bool permissaoSolicitadaAutomaticamente;
    private string ultimaTranscricaoFinalProcessada;
    private string ultimaTranscricaoParcialProcessada;
    private float horarioUltimaTranscricaoFinalProcessada = -999f;
    private float horarioUltimaTranscricaoParcialProcessada = -999f;

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
    public EventoComandoVoz AoTextoParcialVozRecebido => aoTextoParcialVozRecebido;

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
        RegistrarEventosVoiceSDK();
        AtualizarEstadoSelecao();

        if (!cajadoSegurado)
        {
            DefinirStatus(EstadoVozCajado.Desativado, "Escuta nao iniciada: cajado nao esta segurado.");
            return;
        }

        if (!ValidarConfiguracaoVoiceSDK())
            return;

        if (!GarantirPermissaoMicrofone())
            return;

        if (estaEscutando || estaProcessando || appVoiceExperience.MicActive || appVoiceExperience.IsRequestActive)
        {
            DefinirStatus(estadoVoz, "Escuta nao iniciada: Voice SDK ja esta ativo ou processando.");
            return;
        }

        if (!appVoiceExperience.CanActivateAudio() || !appVoiceExperience.CanSend())
        {
            DefinirStatus(EstadoVozCajado.Erro, "Escuta nao iniciada: Voice SDK recusou ativacao.");
            return;
        }

        PararRotinaReativacao();
        ultimaTranscricaoParcialProcessada = string.Empty;
        cancelamentoAtivo = false;

        int sessaoAtual = ++idSessaoVoz;
        VoiceServiceRequest request = appVoiceExperience.Activate(new WitRequestOptions(), CriarEventosSessao(sessaoAtual));

        if (request == null)
        {
            estaEscutando = false;
            estaProcessando = false;
            DefinirStatus(EstadoVozCajado.Erro, "O Voice SDK nao iniciou a requisicao de voz.");
            return;
        }

        estaEscutando = true;
        estaProcessando = false;
        DefinirStatus(EstadoVozCajado.Escutando, "Escuta iniciada. Fale o comando agora.");
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
        DefinirStatus(cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado,
            cajadoSegurado ? "Escuta parada; cajado ainda segurado." : "Escuta parada; cajado solto.");
    }

    public void SolicitarPermissaoMicrofone()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            permissaoMicrofoneConcedida = true;
            DefinirStatus(cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado, "Permissao de microfone ja concedida.");
            return;
        }

        callbacksPermissaoMicrofone = new UnityEngine.Android.PermissionCallbacks();
        callbacksPermissaoMicrofone.PermissionGranted += AoPermissaoMicrofoneConcedida;
        callbacksPermissaoMicrofone.PermissionDenied += AoPermissaoMicrofoneNegada;
        callbacksPermissaoMicrofone.PermissionDeniedAndDontAskAgain += AoPermissaoMicrofoneNegada;

        DefinirStatus(EstadoVozCajado.SolicitandoPermissao, "Solicitando permissao de microfone.");
        UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacksPermissaoMicrofone);
#else
        permissaoMicrofoneConcedida = true;
        DefinirStatus(cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado, "Permissao de microfone liberada no Editor.");
#endif
    }

    public void TestarEntregaDeTranscricao(string texto)
    {
        ProcessarTranscricaoFinal(texto, idSessaoVoz, false, "teste manual");
    }

    [ContextMenu("Testar Voz: Bola de Fogo")]
    private void TestarEntregaBolaDeFogo()
    {
        TestarEntregaDeTranscricao("bola de fogo");
    }

    private VoiceServiceRequestEvents CriarEventosSessao(int sessao)
    {
        VoiceServiceRequestEvents eventos = new VoiceServiceRequestEvents();
        eventos.OnStartListening.AddListener(_ => AoIniciarEscuta(sessao));
        eventos.OnStopListening.AddListener(_ => AoPararEscuta(sessao));
        eventos.OnAudioActivation.AddListener(_ => AoAudioAtivado(sessao));
        eventos.OnAudioDeactivation.AddListener(_ => AoAudioDesativado(sessao));
        eventos.OnSend.AddListener(_ => AoEnviarAudio(sessao));
        eventos.OnPartialTranscription.AddListener(texto => AoReceberTranscricaoParcial(texto, sessao));
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
        DefinirStatus(EstadoVozCajado.Escutando, "Voice SDK iniciou captura do microfone.");
    }

    private void AoPararEscuta(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = true;
        DefinirStatus(EstadoVozCajado.Processando, "Voice SDK parou de escutar e esta processando.");
    }

    private void AoAudioAtivado(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        DefinirStatus(EstadoVozCajado.Escutando, "Audio captado pelo microfone.");
    }

    private void AoAudioDesativado(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        DefinirStatus(estadoVoz, "Audio desativado pelo Voice SDK.");
    }

    private void AoEnviarAudio(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = true;
        DefinirStatus(EstadoVozCajado.Processando, "Audio enviado para reconhecimento.");
    }

    private void AoReceberTranscricaoParcial(string texto, int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        DefinirStatus(EstadoVozCajado.Processando, "Texto parcial recebido.");
        ProcessarTranscricaoParcialRapida(texto, sessao, true);
    }

    private void AoReceberTranscricaoCompleta(string texto, int sessao)
    {
        ProcessarTranscricaoFinal(texto, sessao, true, "request");
    }

    private void AoCancelarSessao(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = false;
        DefinirStatus(cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado, "Sessao de voz cancelada.");
    }

    private void AoFalharSessao(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = false;
        DefinirStatus(EstadoVozCajado.Erro, "Falha ao processar a escuta de voz.");
        AgendarReativacaoAutomatica();
    }

    private void AoCompletarSessao(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = false;
        DefinirStatus(cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado, "Sessao de voz completa.");
        AgendarReativacaoAutomatica();
    }

    private void AoErroVoiceSDK(string tipoErro, string mensagem)
    {
        estaEscutando = false;
        estaProcessando = false;
        DefinirStatus(EstadoVozCajado.Erro, "Erro do Voice SDK.");
    }

    private void AoIniciarEscutaGlobal()
    {
        if (!cajadoSegurado)
            return;

        estaEscutando = true;
        estaProcessando = false;
        DefinirStatus(EstadoVozCajado.Escutando, "VoiceEvents: inicio da escuta.");
    }

    private void AoPararEscutaGlobal()
    {
        if (!cajadoSegurado)
            return;

        estaEscutando = false;
        estaProcessando = true;
        DefinirStatus(EstadoVozCajado.Processando, "VoiceEvents: fim da escuta.");
    }

    private void AoEnviarAudioGlobal()
    {
        if (!cajadoSegurado)
            return;

        DefinirStatus(EstadoVozCajado.Processando, "VoiceEvents: audio enviado.");
    }

    private void AoReceberTranscricaoParcialGlobal(string texto)
    {
        if (!cajadoSegurado)
            return;

        DefinirStatus(EstadoVozCajado.Processando, "VoiceEvents: texto parcial recebido.");
        ProcessarTranscricaoParcialRapida(texto, idSessaoVoz, false);
    }

    private void AoReceberTranscricaoCompletaGlobal(string texto)
    {
        ProcessarTranscricaoFinal(texto, idSessaoVoz, false, "VoiceEvents");
    }

    private void ProcessarTranscricaoFinal(string texto, int sessao, bool exigirSessaoAtual, string origem)
    {
        if (exigirSessaoAtual && !SessaoAtualValida(sessao))
            return;

        if (!isActiveAndEnabled || !cajadoSegurado || cancelamentoAtivo)
            return;

        ultimaTranscricaoBruta = texto ?? string.Empty;
        ultimaTranscricaoNormalizada = NormalizarTranscricao(ultimaTranscricaoBruta);

        if (string.IsNullOrEmpty(ultimaTranscricaoNormalizada))
        {
            DefinirStatus(EstadoVozCajado.Processando, $"Texto final vazio recebido via {origem}.");
            return;
        }

        if (string.Equals(ultimaTranscricaoFinalProcessada, ultimaTranscricaoNormalizada, StringComparison.Ordinal) &&
            Time.time - horarioUltimaTranscricaoFinalProcessada < 0.25f)
        {
            DefinirStatus(EstadoVozCajado.Processando, $"Texto final duplicado ignorado: {ultimaTranscricaoNormalizada}");
            return;
        }

        ultimaTranscricaoFinalProcessada = ultimaTranscricaoNormalizada;
        horarioUltimaTranscricaoFinalProcessada = Time.time;
        DefinirStatus(EstadoVozCajado.Processando, $"Texto entendido via {origem}: {ultimaTranscricaoNormalizada}");
        aoComandoVozRecebido?.Invoke(ultimaTranscricaoNormalizada);
    }

    private void ProcessarTranscricaoParcialRapida(string texto, int sessao, bool exigirSessaoAtual)
    {
        if (exigirSessaoAtual && !SessaoAtualValida(sessao))
            return;

        if (!isActiveAndEnabled || !cajadoSegurado || cancelamentoAtivo)
            return;

        string normalizada = NormalizarTranscricao(texto);
        if (string.IsNullOrEmpty(normalizada))
            return;

        if (string.Equals(ultimaTranscricaoParcialProcessada, normalizada, StringComparison.Ordinal) &&
            Time.time - horarioUltimaTranscricaoParcialProcessada < 0.2f)
        {
            return;
        }

        ultimaTranscricaoParcialProcessada = normalizada;
        horarioUltimaTranscricaoParcialProcessada = Time.time;
        aoTextoParcialVozRecebido?.Invoke(normalizada);
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
        if (!escutarAutomaticamenteAoSegurar || !cajadoSegurado || !isActiveAndEnabled || rotinaReativarEscuta != null)
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

        if (solicitarPermissaoAutomaticamente && !permissaoSolicitadaAutomaticamente)
        {
            permissaoSolicitadaAutomaticamente = true;
            SolicitarPermissaoMicrofone();
        }
        else
        {
            DefinirStatus(EstadoVozCajado.SemPermissaoMicrofone, "Sem permissao de microfone.");
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
        DefinirStatus(cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado, "Permissao de microfone concedida.");

        if (escutarAutomaticamenteAoSegurar && cajadoSegurado && isActiveAndEnabled)
            IniciarEscutaVoz();
    }

    private void AoPermissaoMicrofoneNegada(string permissao)
    {
        if (permissao != UnityEngine.Android.Permission.Microphone)
            return;

        permissaoMicrofoneConcedida = false;
        DefinirStatus(EstadoVozCajado.SemPermissaoMicrofone, "Permissao de microfone negada.");
    }
#endif

    private void RegistrarEventosXR()
    {
        if (interagivelRegistrado != null)
            return;

        if (interagivelXR == null)
        {
            DefinirStatus(EstadoVozCajado.Erro, "CajadoMagicoVoz precisa de um XRBaseInteractable no cajado.");
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
        appVoiceExperience.VoiceEvents.OnStartListening.AddListener(AoIniciarEscutaGlobal);
        appVoiceExperience.VoiceEvents.OnStoppedListening.AddListener(AoPararEscutaGlobal);
        appVoiceExperience.VoiceEvents.OnMicDataSent.AddListener(AoEnviarAudioGlobal);
        appVoiceExperience.VoiceEvents.OnPartialTranscription.AddListener(AoReceberTranscricaoParcialGlobal);
        appVoiceExperience.VoiceEvents.OnFullTranscription.AddListener(AoReceberTranscricaoCompletaGlobal);
        listenersVoiceRegistrados = true;
    }

    private void RemoverEventosVoiceSDK()
    {
        if (!listenersVoiceRegistrados || appVoiceExperience == null)
            return;

        appVoiceExperience.VoiceEvents.OnError.RemoveListener(AoErroVoiceSDK);
        appVoiceExperience.VoiceEvents.OnStartListening.RemoveListener(AoIniciarEscutaGlobal);
        appVoiceExperience.VoiceEvents.OnStoppedListening.RemoveListener(AoPararEscutaGlobal);
        appVoiceExperience.VoiceEvents.OnMicDataSent.RemoveListener(AoEnviarAudioGlobal);
        appVoiceExperience.VoiceEvents.OnPartialTranscription.RemoveListener(AoReceberTranscricaoParcialGlobal);
        appVoiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(AoReceberTranscricaoCompletaGlobal);
        listenersVoiceRegistrados = false;
    }

    private void AoSelecionarCajado(SelectEnterEventArgs args)
    {
        cajadoSegurado = true;
        ultimoInteractor = ObterTransformInteractor(args != null ? args.interactorObject : null);
        cancelamentoAtivo = false;
        idSessaoCancelada = -1;
        DefinirStatus(appVoiceExperience != null ? EstadoVozCajado.Pronto : EstadoVozCajado.SemReferenciaVoiceSDK,
            "Cajado segurado; voz pronta para ativar.");

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
        ultimoInteractor = cajadoSegurado && ultimoInteractor == null ? ObterPrimeiroInteractorSelecionando() : ultimoInteractor;

        if (!cajadoSegurado)
            ultimoInteractor = null;
    }

    private void AtualizarEstadoInicial()
    {
        if (appVoiceExperience == null)
        {
            DefinirStatus(EstadoVozCajado.SemReferenciaVoiceSDK, "AppVoiceExperience ausente.");
            return;
        }

        if (!VoiceSDKTemConfiguracaoValida())
        {
            DefinirStatus(EstadoVozCajado.SemWitConfiguration, "WitConfiguration ausente ou sem Client Access Token.");
            return;
        }

        if (!permissaoMicrofoneConcedida)
        {
            DefinirStatus(EstadoVozCajado.SemPermissaoMicrofone, "Permissao de microfone ausente.");
            return;
        }

        DefinirStatus(cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado,
            cajadoSegurado ? "Voz pronta com cajado segurado." : "Voz pronta; cajado ainda nao segurado.");
    }

    private void EncontrarAppVoiceExperienceSeNecessario()
    {
        if (appVoiceExperience != null)
            return;

        AppVoiceExperience[] encontrados = FindObjectsByType<AppVoiceExperience>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

        if (encontrados != null && encontrados.Length > 0)
            appVoiceExperience = EscolherAppVoiceExperience(encontrados);
    }

    private static AppVoiceExperience EscolherAppVoiceExperience(AppVoiceExperience[] encontrados)
    {
        for (int i = 0; i < encontrados.Length; i++)
        {
            AppVoiceExperience candidato = encontrados[i];
            if (candidato != null && candidato.isActiveAndEnabled)
                return candidato;
        }

        return encontrados[0];
    }

    private bool ValidarConfiguracaoVoiceSDK()
    {
        if (appVoiceExperience == null)
        {
            DefinirStatus(EstadoVozCajado.SemReferenciaVoiceSDK, "AppVoiceExperience ausente.");
            return false;
        }

        if (appVoiceExperience.Configuration == null)
        {
            DefinirStatus(EstadoVozCajado.SemWitConfiguration, "AppVoiceExperience sem WitConfiguration.");
            return false;
        }

        if (string.IsNullOrEmpty(appVoiceExperience.Configuration.GetClientAccessToken()))
        {
            DefinirStatus(EstadoVozCajado.SemWitConfiguration, "WitConfiguration sem Client Access Token.");
            return false;
        }

        return true;
    }

    private bool VoiceSDKTemConfiguracaoValida()
    {
        return appVoiceExperience != null
            && appVoiceExperience.Configuration != null
            && !string.IsNullOrEmpty(appVoiceExperience.Configuration.GetClientAccessToken());
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

            if (char.IsWhiteSpace(caractere) || EhSeparadorDeFrase(caractere))
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

        return builder.ToString().Trim();
    }

    private static bool EhSeparadorDeFrase(char caractere)
    {
        return caractere == '-' || caractere == '_' || caractere == '.' ||
               caractere == ',' || caractere == '!' || caractere == '?' ||
               caractere == ';' || caractere == ':';
    }

    private void DefinirStatus(EstadoVozCajado novoEstado, string mensagem)
    {
        estadoVoz = novoEstado;
    }
}
