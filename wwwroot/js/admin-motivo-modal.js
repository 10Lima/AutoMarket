// wwwroot/js/admin-motivo-modal.js
// Script global para modal de motivos de ações de admin

(function () {
    'use strict';

    function init() {
        const modalEl = document.getElementById('motivoModal');
        if (!modalEl) {
            console.warn('[admin-motivo-modal] Modal #motivoModal não encontrado.');
            return;
        }

        const modal = new bootstrap.Modal(modalEl);

        const form = document.getElementById('motivoForm');
        const titulo = document.getElementById('motivoTitulo');
        const idUtilizador = document.getElementById('motivoIdUtilizador');
        const motivo = document.getElementById('motivoTexto');
        const sugestoes = document.getElementById('motivosSugestoes');
        const sugestoesArea = document.getElementById('motivosSugestoesArea');
        const erro = document.getElementById('motivoErro');
        const counter = document.getElementById('motivoCounter');
        const obrigatorio = document.getElementById('motivoObrigatorio');
        const submitBtn = document.getElementById('motivoSubmit');

        // ? Configurações por tipo de ação (nomes exatos da BD)
        const presets = {
            bloquear: {
                titulo: 'Bloquear utilizador',
                obrigatorio: true,
                btnClass: 'btn-danger',
                btnText: 'Bloquear',
                sugestoes: [
                    "Violação dos termos de utilização.",
                    "Atividade suspeita detetada na conta.",
                    "Conteúdo inadequado ou fraudulento.",
                    "Comportamento abusivo ou assédio."
                ]
            },
            desbloquear: {
                titulo: 'Desbloquear utilizador',
                obrigatorio: false,
                btnClass: 'btn-success',
                btnText: 'Desbloquear',
                sugestoes: [
                    "Situação regularizada.",
                    "Pedido de revisão aceite.",
                    "Erro administrativo corrigido."
                ]
            },
            rejeitar_vendedor: {
                titulo: 'Rejeitar candidatura a vendedor',
                obrigatorio: true,
                btnClass: 'btn-danger',
                btnText: 'Rejeitar',
                sugestoes: [
                    "Documentação/identificação inválida ou incompleta.",
                    "Inconsistência nos dados fornecidos.",
                    "Candidatura não cumpre os critérios da plataforma.",
                    "NIF/NIPC inválido."
                ]
            },
            remover_admin: {
                titulo: 'Remover privilégios de administrador',
                obrigatorio: true,
                btnClass: 'btn-warning',
                btnText: 'Remover',
                sugestoes: [
                    "Cessação de funções na plataforma.",
                    "Acesso administrativo já não é necessário.",
                    "Violação das políticas internas.",
                    "Pedido do próprio utilizador."
                ]
            },
            recuperar_admin: { // ✅ NOVO
                titulo: 'Recuperar administrador',
                obrigatorio: false,
                btnClass: 'btn-primary',
                btnText: 'Recuperar',
                sugestoes: [
                    "Reativação após validação interna.",
                    "Reposição de permissões administrativas.",
                    "Correção de remoção indevida."
                ]
            }
        };

        let postUrl = null;
        let acaoAtual = null;
        let onSuccess = null;

        // Contador de caracteres
        if (motivo && counter) {
            motivo.addEventListener('input', function () {
                counter.textContent = (this.value || "").length;
            });
        }

        // ? Função global para abrir modal
        window.abrirMotivoAdmin = function (acao, url, idU, onOk) {
            const config = presets[acao];
            if (!config) {
                console.error('[abrirMotivoAdmin] Ação desconhecida:', acao, '| Tipos válidos:', Object.keys(presets));
                return;
            }

            postUrl = url;
            acaoAtual = acao;
            onSuccess = (typeof onOk === "function") ? onOk : null;

            titulo.textContent = config.titulo;
            submitBtn.className = `btn ${config.btnClass}`;
            submitBtn.textContent = config.btnText;

            obrigatorio.style.display = config.obrigatorio ? 'inline' : 'none';
            motivo.required = config.obrigatorio;

            idUtilizador.value = idU || "";
            motivo.value = "";
            counter.textContent = "0";
            erro.style.display = "none";
            erro.textContent = "";
            sugestoes.innerHTML = "";

            const lista = config.sugestoes || [];
            sugestoesArea.style.display = lista.length ? '' : 'none';

            lista.forEach(txt => {
                const b = document.createElement('button');
                b.type = "button";
                b.className = "btn btn-sm btn-outline-secondary";
                b.textContent = txt;
                b.addEventListener('click', () => {
                    motivo.value = txt;
                    counter.textContent = txt.length;
                    motivo.focus();
                });
                sugestoes.appendChild(b);
            });

            submitBtn.disabled = false;
            modal.show();
        };

        // Submit AJAX
        if (form) {
            form.addEventListener('submit', function (e) {
                e.preventDefault();

                const config = presets[acaoAtual];
                const m = (motivo.value || "").trim();

                if (config && config.obrigatorio && !m) {
                    erro.textContent = "Motivo é obrigatório.";
                    erro.style.display = "block";
                    return;
                }
                if (m.length > 400) {
                    erro.textContent = "O motivo não pode ter mais de 400 caracteres.";
                    erro.style.display = "block";
                    return;
                }

                submitBtn.disabled = true;
                const originalText = config ? config.btnText : "Confirmar";
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>A processar...';

                $.ajax({
                    url: postUrl,
                    method: "POST",
                    data: $(form).serialize(),
                    success: function () {
                        modal.hide();
                        if (onSuccess) onSuccess();
                        else location.reload();
                    },
                    error: function (xhr) {
                        erro.textContent = xhr?.responseText || "Erro ao executar a ação.";
                        erro.style.display = "block";
                        submitBtn.disabled = false;
                        submitBtn.textContent = originalText;
                    }
                });
            });
        }

        // Reset ao fechar
        modalEl.addEventListener('hidden.bs.modal', function () {
            form.reset();
            submitBtn.disabled = false;
            erro.style.display = "none";
            erro.textContent = "";
            counter.textContent = "0";
            sugestoes.innerHTML = "";
            postUrl = null;
            acaoAtual = null;
            onSuccess = null;
        });

        console.log('[admin-motivo-modal] Inicializado com sucesso. window.abrirMotivoAdmin está disponível.');
    }

    // Inicializar quando DOM estiver pronto
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();