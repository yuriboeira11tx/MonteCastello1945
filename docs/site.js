"use strict";

// As marcações são pessoais e locais; abrir um formulário não significa enviá-lo.
const progressKey = "mc1945-participacao-v1";
const checkboxes = [...document.querySelectorAll("[data-completion]")];
const status = document.querySelector("#participation-status");
const reset = document.querySelector("#reset-progress");
let stored = {};
try {
  const saved = JSON.parse(localStorage.getItem(progressKey) || "{}");
  if (saved && typeof saved === "object" && !Array.isArray(saved)) stored = saved;
} catch {
  document.querySelector("#storage-note").textContent = "As marcações valem apenas para esta visita. Elas não enviam dados. Para enviar suas respostas, use o botão Enviar de cada formulário.";
}

function updateProgress(save = false) {
  const current = {};
  for (const checkbox of checkboxes) {
    current[checkbox.dataset.completion] = checkbox.checked;
    checkbox.closest(".step").classList.toggle("is-done", checkbox.checked);
  }
  const count = checkboxes.filter(checkbox => checkbox.checked).length;
  status.textContent = count === 3
    ? "Percurso marcado como concluído. Obrigado por participar da pesquisa!"
    : count === 0 ? "Marque as etapas conforme concluir." : `${count} de 3 etapas marcadas como concluídas.`;
  reset.hidden = count === 0;
  if (save) {
    try { localStorage.setItem(progressKey, JSON.stringify(current)); }
    catch { /* A página continua funcionando mesmo sem armazenamento local. */ }
  }
}
for (const checkbox of checkboxes) {
  checkbox.checked = stored[checkbox.dataset.completion] === true;
  checkbox.addEventListener("change", () => updateProgress(true));
}
reset.addEventListener("click", () => {
  checkboxes.forEach(checkbox => { checkbox.checked = false; });
  updateProgress(true);
  checkboxes[0].focus();
});
updateProgress();

// O endereço geral continua utilizável se a API estiver indisponível ou limitada.
async function showRelease() {
  const label = document.querySelector("#release-status");
  try {
    const response = await fetch("https://api.github.com/repos/yuriboeira11tx/MonteCastello1945/releases?per_page=10", {
      headers: { Accept: "application/vnd.github+json" },
      signal: AbortSignal.timeout(8000)
    });
    if (!response.ok) return;
    const releases = await response.json();
    if (!Array.isArray(releases)) return;
    const release = releases.find(item => !item.draft && item.assets?.some(asset => /\.(zip|rar|7z|exe)$/i.test(asset.name)));
    if (!release) {
      label.textContent = "O pacote do jogo ainda não está disponível nos Releases. Aguarde a publicação para fazer o download.";
      return;
    }
    const url = new URL(release.html_url);
    if (url.origin !== "https://github.com" || !url.pathname.startsWith("/yuriboeira11tx/MonteCastello1945/releases/")) return;
    document.querySelector("#release-link").href = url.href;
    label.textContent = `Disponível: ${release.name || release.tag_name}${release.prerelease ? " · versão de teste" : ""}. Veja o pacote e as instruções no Release.`;
  } catch {
    // Sem conexão ou suporte a timeout: mantém o link estático para Releases.
  }
}
showRelease();
