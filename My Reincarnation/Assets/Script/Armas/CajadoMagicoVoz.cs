using System;
using System.Collections;
using System.Text;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data;
using Meta.WitAi.Lib;
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
        SemDispositivoMicrofone,
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
    [SerializeField] private bool usarSomenteMicrofoneDoQuest = true;
    [SerializeField] private bool preferirMicrofoneDoOculus = true;
    [SerializeField] private string nomeParcialMicrofonePreferido = "Oculus";
    [SerializeField, Min(1f)] private float tempoDiagnosticoSemAudio = 3f;

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

    [Header("Diagnostico de Voz")]
    [SerializeField] private bool mostrarDiagnosticoNoConsole = true;
    [SerializeField, TextArea(2, 5)] private string statusDiagnosticoVoz;
    [SerializeField] private string ultimoErroVoiceSDK;
    [SerializeField] private int quantidadeDispositivosMicrofone;
    [SerializeField] private string dispositivosMicrofoneDetectados;
    [SerializeField] private string microfoneSelecionado;
    [SerializeField] private bool capturaAudioAtivada;
    [SerializeField] private bool amostrasAudioRecebidas;
    [SerializeField] private bool audioEnviadoParaReconhecimento;
    [SerializeField] private bool transcricaoParcialRecebida;
    [SerializeField] private bool transcricaoFinalRecebida;
    [SerializeField] private bool comandoEntregueAoInterpretador;
    [SerializeField] private bool vozDetectadaNoMicrofone;
    [SerializeField] private bool comandoBolaDeFogoOuvido;
    [SerializeField, Range(0f, 1f)] private float ultimoNivelMicrofone;
    [SerializeField, Range(0f, 1f)] private float maiorNivelMicrofone;

    private XRBaseInteractable interagivelXR;
    private XRBaseInteractable interagivelRegistrado;
    private Coroutine rotinaReativarEscuta;
    private Coroutine rotinaDiagnosticoAudio;
    private int idSessaoVoz;
    private int idSessaoCancelada = -1;
    private bool cancelamentoAtivo;
    private bool listenersVoiceRegistrados;
    private bool permissaoSolicitadaAutomaticamente;
    private string ultimaTranscricaoFinalProcessada;
    private string ultimaTranscricaoParcialProcessada;
    private float horarioUltimaTranscricaoFinalProcessada = -999f;
    private float horarioUltimaTranscricaoParcialProcessada = -999f;
    private static CajadoMagicoVoz instanciaComEscutaAtiva;

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
    public string StatusDiagnosticoVoz => statusDiagnosticoVoz;
    public EventoComandoVoz AoComandoVozRecebido => aoComandoVozRecebido;
    public EventoComandoVoz AoTextoParcialVozRecebido => aoTextoParcialVozRecebido;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarDonoGlobalDaEscuta()
    {
        instanciaComEscutaAtiva = null;
    }

    private void Awake()
    {
        EncontrarAppVoiceExperienceSeNecessario();
        EncontrarInteragivelXRSeNecessario();
        AtualizarPermissaoMicrofone();
        AtualizarDiagnosticoMicrofone();
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
        AtualizarDiagnosticoMicrofone();
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
        tempoDiagnosticoSemAudio = Mathf.Max(1f, tempoDiagnosticoSemAudio);
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

        ReiniciarDiagnosticoSessao();

        if (!ValidarConfiguracaoVoiceSDK())
            return;

        if (!ValidarDispositivoMicrofone())
            return;

        if (!GarantirPermissaoMicrofone())
            return;

        if (instanciaComEscutaAtiva != null && instanciaComEscutaAtiva != this)
        {
            if (instanciaComEscutaAtiva.cajadoSegurado &&
                (instanciaComEscutaAtiva.estaEscutando ||
                 instanciaComEscutaAtiva.estaProcessando))
            {
                DefinirStatus(
                    EstadoVozCajado.Erro,
                    $"Voice SDK ocupado por outro cajado: {instanciaComEscutaAtiva.name}.");
                return;
            }

            instanciaComEscutaAtiva = null;
        }

        if (estaEscutando || estaProcessando || appVoiceExperience.MicActive || appVoiceExperience.IsRequestActive)
        {
            DefinirStatus(estadoVoz, "Escuta nao iniciada: Voice SDK ja esta ativo ou processando.");
            return;
        }

        if (!ConfigurarMicrofonePreferido())
            return;

        string erroAtivacaoAudio = appVoiceExperience.GetActivateAudioError();
        string erroEnvio = appVoiceExperience.GetSendError();
        if (!string.IsNullOrEmpty(erroAtivacaoAudio) || !string.IsNullOrEmpty(erroEnvio))
        {
            string motivo = !string.IsNullOrEmpty(erroAtivacaoAudio)
                ? erroAtivacaoAudio
                : erroEnvio;
            DefinirStatus(EstadoVozCajado.Erro, $"Escuta nao iniciada: {motivo}");
            return;
        }

        PararRotinaReativacao();
        ultimaTranscricaoParcialProcessada = string.Empty;
        cancelamentoAtivo = false;

        int sessaoAtual = ++idSessaoVoz;
        instanciaComEscutaAtiva = this;
        VoiceServiceRequest request = appVoiceExperience.Activate(new WitRequestOptions(), CriarEventosSessao(sessaoAtual));

        if (request == null)
        {
            if (instanciaComEscutaAtiva == this)
                instanciaComEscutaAtiva = null;

            estaEscutando = false;
            estaProcessando = false;
            DefinirStatus(EstadoVozCajado.Erro, "O Voice SDK nao iniciou a requisicao de voz.");
            return;
        }

        estaEscutando = true;
        estaProcessando = false;
        DefinirStatus(EstadoVozCajado.Escutando, "Escuta iniciada. Fale o comando agora.");
        IniciarDiagnosticoTemporizadoAudio();
    }

    public void PararEscutaVoz()
    {
        PararRotinaReativacao();
        PararDiagnosticoTemporizadoAudio();
        cancelamentoAtivo = true;
        idSessaoCancelada = idSessaoVoz;

        bool possuiEscutaGlobal = instanciaComEscutaAtiva == this;
        if (possuiEscutaGlobal &&
            appVoiceExperience != null &&
            (appVoiceExperience.MicActive || appVoiceExperience.Active || appVoiceExperience.IsRequestActive))
        {
            appVoiceExperience.DeactivateAndAbortRequest();
        }

        if (possuiEscutaGlobal)
            instanciaComEscutaAtiva = null;

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

        capturaAudioAtivada = true;
        DefinirStatus(EstadoVozCajado.Escutando, "Captura de audio ativada pelo Voice SDK.");
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
        audioEnviadoParaReconhecimento = true;
        DefinirStatus(EstadoVozCajado.Processando, "Audio enviado para reconhecimento.");
    }

    private void AoReceberTranscricaoParcial(string texto, int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        transcricaoParcialRecebida = true;
        DefinirStatus(EstadoVozCajado.Processando, "Texto parcial recebido.");
        ProcessarTranscricaoParcialRapida(texto, sessao, true);
    }

    private void AoReceberTranscricaoCompleta(string texto, int sessao)
    {
        transcricaoFinalRecebida = true;
        ProcessarTranscricaoFinal(texto, sessao, true, "request");
    }

    private void AoCancelarSessao(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = false;
        PararDiagnosticoTemporizadoAudio();
        LiberarDonoGlobalEscuta();
        DefinirStatus(cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado, "Sessao de voz cancelada.");
    }

    private void AoFalharSessao(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = false;
        PararDiagnosticoTemporizadoAudio();
        LiberarDonoGlobalEscuta();
        DefinirStatus(EstadoVozCajado.Erro, "Falha ao processar a escuta de voz.");
        AgendarReativacaoAutomatica();
    }

    private void AoCompletarSessao(int sessao)
    {
        if (!SessaoAtualValida(sessao))
            return;

        estaEscutando = false;
        estaProcessando = false;
        PararDiagnosticoTemporizadoAudio();
        LiberarDonoGlobalEscuta();
        DefinirStatus(cajadoSegurado ? EstadoVozCajado.Pronto : EstadoVozCajado.Desativado, "Sessao de voz completa.");
        AgendarReativacaoAutomatica();
    }

    private void AoErroVoiceSDK(string tipoErro, string mensagem)
    {
        if (instanciaComEscutaAtiva != this)
            return;

        estaEscutando = false;
        estaProcessando = false;
        PararDiagnosticoTemporizadoAudio();
        LiberarDonoGlobalEscuta();
        ultimoErroVoiceSDK = $"{tipoErro}: {mensagem}".Trim();
        DefinirStatus(EstadoVozCajado.Erro, $"Erro do Voice SDK: {ultimoErroVoiceSDK}");
    }

    private void AoIniciarEscutaGlobal()
    {
        if (!EhDonoGlobalEscuta())
            return;

        estaEscutando = true;
        estaProcessando = false;
        DefinirStatus(EstadoVozCajado.Escutando, "VoiceEvents: inicio da escuta.");
    }

    private void AoPararEscutaGlobal()
    {
        if (!EhDonoGlobalEscuta())
            return;

        estaEscutando = false;
        estaProcessando = true;
        DefinirStatus(EstadoVozCajado.Processando, "VoiceEvents: fim da escuta.");
    }

    private void AoEnviarAudioGlobal()
    {
        if (!EhDonoGlobalEscuta())
            return;

        audioEnviadoParaReconhecimento = true;
        DefinirStatus(EstadoVozCajado.Processando, "VoiceEvents: audio enviado.");
    }

    private void AoReceberTranscricaoParcialGlobal(string texto)
    {
        if (!EhDonoGlobalEscuta())
            return;

        transcricaoParcialRecebida = true;
        DefinirStatus(EstadoVozCajado.Processando, "VoiceEvents: texto parcial recebido.");
        ProcessarTranscricaoParcialRapida(texto, idSessaoVoz, false);
    }

    private void AoReceberTranscricaoCompletaGlobal(string texto)
    {
        if (!EhDonoGlobalEscuta())
            return;

        transcricaoFinalRecebida = true;
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
        comandoBolaDeFogoOuvido = EhComandoBolaDeFogo(ultimaTranscricaoNormalizada);
        Debug.Log(
            comandoBolaDeFogoOuvido
                ? $"[VOZ CAJADO] COMANDO RECEBIDO: \"{ultimaTranscricaoNormalizada}\" (Bola de Fogo reconhecida)."
                : $"[VOZ CAJADO] FALA RECEBIDA: \"{ultimaTranscricaoNormalizada}\".",
            this);
        DefinirStatus(EstadoVozCajado.Processando, $"Texto entendido via {origem}: {ultimaTranscricaoNormalizada}");
        comandoEntregueAoInterpretador = true;
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

        if (EhComandoBolaDeFogo(normalizada))
            Debug.Log($"[VOZ CAJADO] COMANDO OUVIDO (parcial): \"{normalizada}\".", this);

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

    private void IniciarDiagnosticoTemporizadoAudio()
    {
        PararDiagnosticoTemporizadoAudio();
        rotinaDiagnosticoAudio = StartCoroutine(VerificarRecepcaoAudioDepoisDoAtraso());
    }

    private IEnumerator VerificarRecepcaoAudioDepoisDoAtraso()
    {
        yield return new WaitForSeconds(tempoDiagnosticoSemAudio);
        rotinaDiagnosticoAudio = null;

        if (!EhDonoGlobalEscuta() || !estaEscutando)
            yield break;

        if (!amostrasAudioRecebidas)
        {
            DefinirStatus(
                EstadoVozCajado.Escutando,
                $"Microfone '{microfoneSelecionado}' abriu, mas nao entregou amostras de audio em {tempoDiagnosticoSemAudio:0.#}s.");
            yield break;
        }

        if (maiorNivelMicrofone <= 0.0001f)
        {
            DefinirStatus(
                EstadoVozCajado.Escutando,
                $"Microfone '{microfoneSelecionado}' entrega amostras, mas o nivel permanece zerado. " +
                "Verifique se o Headset Microphone/Oculus Virtual Audio Device foi colocado em Mudo pelo Windows ou Meta Quest Link.");
            yield break;
        }

        DefinirStatus(
            EstadoVozCajado.Escutando,
            $"Audio confirmado no microfone '{microfoneSelecionado}'. Nivel maximo: {maiorNivelMicrofone:0.0000}.");
    }

    private void PararDiagnosticoTemporizadoAudio()
    {
        if (rotinaDiagnosticoAudio == null)
            return;

        StopCoroutine(rotinaDiagnosticoAudio);
        rotinaDiagnosticoAudio = null;
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
        appVoiceExperience.VoiceEvents.OnMicLevelChanged.AddListener(AoNivelMicrofoneAlterado);
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
        appVoiceExperience.VoiceEvents.OnMicLevelChanged.RemoveListener(AoNivelMicrofoneAlterado);
        appVoiceExperience.VoiceEvents.OnPartialTranscription.RemoveListener(AoReceberTranscricaoParcialGlobal);
        appVoiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(AoReceberTranscricaoCompletaGlobal);
        listenersVoiceRegistrados = false;
    }

    private void AoSelecionarCajado(SelectEnterEventArgs args)
    {
        IXRInteractor interactor = args != null ? args.interactorObject : null;
        if (!InteractorEhMaoValida(interactor))
        {
            AtualizarEstadoSelecao();
            DefinirStatus(estadoVoz, "Selecao por socket/inventario ignorada pelo sistema de voz.");
            return;
        }

        cajadoSegurado = true;
        ultimoInteractor = ObterTransformInteractor(interactor);
        cancelamentoAtivo = false;
        idSessaoCancelada = -1;
        DefinirStatus(appVoiceExperience != null ? EstadoVozCajado.Pronto : EstadoVozCajado.SemReferenciaVoiceSDK,
            "Cajado segurado; voz pronta para ativar.");

        if (escutarAutomaticamenteAoSegurar)
            IniciarEscutaVoz();
    }

    private void AoSoltarCajado(SelectExitEventArgs args)
    {
        IXRInteractor interactorSaindo = args != null ? args.interactorObject : null;
        Transform outraMao = ObterPrimeiroInteractorSelecionando(interactorSaindo);

        if (outraMao != null)
        {
            cajadoSegurado = true;
            ultimoInteractor = outraMao;
            return;
        }

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

        Transform interactorMao = ObterPrimeiroInteractorSelecionando();
        cajadoSegurado = interactorMao != null;
        ultimoInteractor = cajadoSegurado ? interactorMao : null;

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

    private Transform ObterPrimeiroInteractorSelecionando(IXRInteractor interactorIgnorado = null)
    {
        if (interagivelXR == null || interagivelXR.interactorsSelecting == null || interagivelXR.interactorsSelecting.Count == 0)
            return null;

        for (int i = 0; i < interagivelXR.interactorsSelecting.Count; i++)
        {
            IXRInteractor interactor = interagivelXR.interactorsSelecting[i];
            if (interactor == interactorIgnorado || !InteractorEhMaoValida(interactor))
                continue;

            return ObterTransformInteractor(interactor);
        }

        return null;
    }

    private static Transform ObterTransformInteractor(IXRInteractor interactor)
    {
        return interactor is Component componente ? componente.transform : null;
    }

    private static bool InteractorEhMaoValida(IXRInteractor interactor)
    {
        if (interactor == null || InteractorEhSocketOuInventario(interactor))
            return false;

        if (interactor is XRDirectInteractor || interactor is XRRayInteractor)
            return true;

        Transform atual = ObterTransformInteractor(interactor);
        while (atual != null)
        {
            string nome = atual.name.ToLowerInvariant();
            if (nome.Contains("left") || nome.Contains("right") ||
                nome.Contains("esquerda") || nome.Contains("direita") ||
                nome.Contains("hand") || nome.Contains("mao") ||
                nome.Contains("controller") || nome.Contains("controlador"))
            {
                return true;
            }

            atual = atual.parent;
        }

        return false;
    }

    private static bool InteractorEhSocketOuInventario(IXRInteractor interactor)
    {
        if (interactor is XRSocketInteractor)
            return true;

        Transform atual = ObterTransformInteractor(interactor);
        while (atual != null)
        {
            string nome = atual.name.ToLowerInvariant();
            if (nome.Contains("socket") || nome.Contains("slot") ||
                nome.Contains("inventario") || nome.Contains("inventory"))
            {
                return true;
            }

            atual = atual.parent;
        }

        return false;
    }

    private void AoNivelMicrofoneAlterado(float nivel)
    {
        if (!EhDonoGlobalEscuta())
            return;

        ultimoNivelMicrofone = Mathf.Clamp01(nivel);
        maiorNivelMicrofone = Mathf.Max(maiorNivelMicrofone, ultimoNivelMicrofone);
        amostrasAudioRecebidas = true;

        if (!vozDetectadaNoMicrofone && ultimoNivelMicrofone > 0.001f)
        {
            vozDetectadaNoMicrofone = true;
            Debug.Log(
                $"[VOZ CAJADO] AUDIO OUVIDO pelo microfone do Quest. Nivel: {ultimoNivelMicrofone:0.0000}.",
                this);
        }
    }

    private void ReiniciarDiagnosticoSessao()
    {
        capturaAudioAtivada = false;
        amostrasAudioRecebidas = false;
        audioEnviadoParaReconhecimento = false;
        transcricaoParcialRecebida = false;
        transcricaoFinalRecebida = false;
        comandoEntregueAoInterpretador = false;
        vozDetectadaNoMicrofone = false;
        comandoBolaDeFogoOuvido = false;
        ultimoNivelMicrofone = 0f;
        maiorNivelMicrofone = 0f;
        ultimoErroVoiceSDK = string.Empty;
    }

    private void AtualizarDiagnosticoMicrofone()
    {
        string[] dispositivos = Microphone.devices;
        quantidadeDispositivosMicrofone = dispositivos != null ? dispositivos.Length : 0;
        dispositivosMicrofoneDetectados = quantidadeDispositivosMicrofone > 0
            ? string.Join(", ", dispositivos)
            : "Nenhum dispositivo detectado pelo Unity.";
    }

    private bool ValidarDispositivoMicrofone()
    {
        AtualizarDiagnosticoMicrofone();

#if UNITY_EDITOR || UNITY_STANDALONE
        if (quantidadeDispositivosMicrofone <= 0)
        {
            DefinirStatus(
                EstadoVozCajado.SemDispositivoMicrofone,
                "Nenhum microfone foi detectado. Verifique o dispositivo de entrada e a permissao de microfone do Windows.");
            return false;
        }
#endif

        return true;
    }

    private bool ConfigurarMicrofonePreferido()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        microfoneSelecionado = "Microfone nativo do Meta Quest (Android)";
        DefinirStatus(EstadoVozCajado.Pronto, $"Microfone selecionado: {microfoneSelecionado}");
        return true;
#else
        AudioBuffer buffer = AudioBuffer.Instance;
        if (buffer == null || !(buffer.MicInput is Mic mic))
        {
            DefinirStatus(
                EstadoVozCajado.Erro,
                "AudioBuffer do Voice SDK nao disponibilizou uma entrada de microfone compativel.");
            return false;
        }

        var dispositivos = mic.Devices;
        if (dispositivos == null || dispositivos.Count == 0)
        {
            DefinirStatus(
                EstadoVozCajado.SemDispositivoMicrofone,
                "Voice SDK nao encontrou microfones na lista interna.");
            return false;
        }

        int indiceEscolhido = -1;
        string trechoPreferido = preferirMicrofoneDoOculus
            ? "Oculus"
            : nomeParcialMicrofonePreferido;

        if (!string.IsNullOrWhiteSpace(trechoPreferido))
        {
            for (int i = 0; i < dispositivos.Count; i++)
            {
                if (dispositivos[i].IndexOf(trechoPreferido, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                indiceEscolhido = i;
                break;
            }
        }

        if (indiceEscolhido < 0 || indiceEscolhido >= dispositivos.Count)
        {
            if (usarSomenteMicrofoneDoQuest)
            {
                DefinirStatus(
                    EstadoVozCajado.SemDispositivoMicrofone,
                    $"Microfone do Quest/Oculus nao encontrado. Dispositivos detectados: {string.Join(", ", dispositivos)}");
                return false;
            }

            indiceEscolhido = mic.CurrentDeviceIndex;
            if (indiceEscolhido < 0 || indiceEscolhido >= dispositivos.Count)
                indiceEscolhido = 0;
        }

        if (mic.CurrentDeviceIndex != indiceEscolhido)
            mic.ChangeMicDevice(indiceEscolhido);

        microfoneSelecionado = dispositivos[indiceEscolhido];
        DefinirStatus(EstadoVozCajado.Pronto, $"Microfone selecionado: {microfoneSelecionado}");
        return true;
#endif
    }

    private bool EhDonoGlobalEscuta()
    {
        return instanciaComEscutaAtiva == this && cajadoSegurado;
    }

    private void LiberarDonoGlobalEscuta()
    {
        if (instanciaComEscutaAtiva == this)
            instanciaComEscutaAtiva = null;
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

    private static bool EhComandoBolaDeFogo(string textoNormalizado)
    {
        if (string.IsNullOrEmpty(textoNormalizado))
            return false;

        return textoNormalizado.IndexOf("bola de fogo", StringComparison.Ordinal) >= 0 ||
               textoNormalizado.IndexOf("bola fogo", StringComparison.Ordinal) >= 0 ||
               textoNormalizado.IndexOf("fireball", StringComparison.Ordinal) >= 0 ||
               textoNormalizado.IndexOf("fire ball", StringComparison.Ordinal) >= 0;
    }

    private void DefinirStatus(EstadoVozCajado novoEstado, string mensagem)
    {
        estadoVoz = novoEstado;
        statusDiagnosticoVoz = string.IsNullOrWhiteSpace(mensagem)
            ? novoEstado.ToString()
            : $"{novoEstado}: {mensagem}";

        if (mostrarDiagnosticoNoConsole)
            Debug.Log($"[VOZ CAJADO] {statusDiagnosticoVoz}", this);
    }
}
