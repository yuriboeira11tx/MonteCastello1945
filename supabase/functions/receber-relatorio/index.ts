// POST { jwt: <relatório RS256 emitido pelo jogo> }.
// 200: recibo; 400/401: inválido; 409: identificação já usada com outros dados.
// A chave administrativa permanece no ambiente da função, nunca no jogo.
const keyId = "feb-report-1113d5bbac4e";
const publicKey = {
  kty: "RSA", e: "AQAB", alg: "RS256", ext: true,
  n: "o6F-HuHR9-W6830bCYDN7t4R8UnFp2jvo37H9vxzj6Kq40dIncfoHXmd7JtzdCpe5qhNnruaTLM8YniJWF8A6lfiHYkgMMiGiccgUs-ZYpPWcCUpMPIpURW5vjKmGBvE_Cp28SbPYZr-7nipzHeUblSoZ-cix55S4sQ9KfF4bPhd6PEzhhnM87JqqpOnZNuhGda7T-hEqXBnSNzJhg3yf7WivWxOfx4Y5k9bf5LsQD99aOG2-9suGfXssnQXv9v8WQ5JrceBPSIXI-pLPuFeI-8swfzKjGobtmdoNeDN8Flmg_s10jt22ID9S9fjfoCAZzFp0QfNEzQOeNJTczhXCQ"
};
const key = crypto.subtle.importKey("jwk", publicKey,
  {name:"RSASSA-PKCS1-v1_5", hash:"SHA-256"}, false, ["verify"]);
const encoder = new TextEncoder();
const headers = {
  "Content-Type":"application/json",
  "Access-Control-Allow-Origin":"*",
  "Access-Control-Allow-Headers":"content-type, apikey, authorization",
  "Access-Control-Allow-Methods":"POST, OPTIONS",
  "Cache-Control":"no-store"
};
class RequestError extends Error {
  constructor(message, status = 400) { super(message); this.status = status; }
}
const reply = (body, status = 200) => new Response(JSON.stringify(body), {status, headers});
const decode = (value) => {
  if (!/^[A-Za-z0-9_-]+$/.test(value)) throw new RequestError("Código inválido.", 401);
  return Uint8Array.from(atob(value.replace(/-/g,"+").replace(/_/g,"/").padEnd(Math.ceil(value.length/4)*4,"=")), c=>c.charCodeAt(0));
};
const digest = async value => new Uint8Array(await crypto.subtle.digest("SHA-256",encoder.encode(value)));
const hex = bytes => Array.from(bytes, b=>b.toString(16).padStart(2,"0")).join("");
function ensure(condition, message = "Relatório incompleto ou inválido.") {
  if (!condition) throw new RequestError(message);
}
async function expectedCode(id) {
  const bytes = await digest(id), alphabet="0123456789ABCDEFGHJKMNPQRSTVWXYZ";
  let body="", checksum=0;
  for (let i=0;i<12;i++) {
    const bit=i*5, index=Math.floor(bit/8), offset=bit%8;
    const value=((bytes[index]<<8|bytes[index+1])>>(11-offset))&31;
    body+=alphabet[value]; checksum=(checksum*32+value)%1021;
  }
  body+=alphabet[checksum>>5]+alphabet[checksum&31];
  return `FEB-${body.slice(0,4)}-${body.slice(4,8)}-${body.slice(8,12)}-${body.slice(12)}`;
}
async function verify(jwt) {
  ensure(typeof jwt==="string" && jwt.length<=524288, "Código ausente ou grande demais.");
  const parts=jwt.split(".");
  ensure(parts.length===3, "Envie o relatório assinado pelo jogo.");
  let header, claims;
  try {
    header=JSON.parse(new TextDecoder("utf-8",{fatal:true}).decode(decode(parts[0])));
    claims=JSON.parse(new TextDecoder("utf-8",{fatal:true}).decode(decode(parts[1])));
  } catch { throw new RequestError("Código inválido.",401); }
  if(header.alg!=="RS256" || header.typ!=="JWT" || header.kid!==keyId ||
    !await crypto.subtle.verify("RSASSA-PKCS1-v1_5",await key,decode(parts[2]),encoder.encode(parts[0]+"."+parts[1])))
    throw new RequestError("Assinatura do relatório inválida.",401);
  const r=claims.report;
  ensure(claims.v===1 && claims.iss==="MonteCastello1945" && claims.aud==="professor-relatorios" &&
    r?.schemaVersion===1 && claims.jti===r.reportId && /^[0-9a-f]{32}$/.test(r.reportId));
  ensure(r.code===await expectedCode(r.reportId));
  const date=Date.parse(r.completedUtc);
  ensure(Number.isFinite(date) && Number.isSafeInteger(claims.iat) && claims.iat===Math.floor(date/1000) &&
    date<=Date.now()+86400000);
  ensure(typeof r.studentName==="string" && r.studentName.trim().length>0 &&
    r.studentName.length<=120 && !/[\x00-\x1f\x7f]/.test(r.studentName));
  ensure(typeof r.buildVersion==="string" && r.buildVersion.length<=80 &&
    typeof r.sessionId==="string" && r.sessionId.length<=128 &&
    typeof r.questionnaireDigest==="string" && /^[0-9a-f]{64}$/.test(r.questionnaireDigest));
  for (const field of ["testSession","fieldTimeIncomplete","answerHistoryIncomplete"])
    ensure(typeof r[field]==="boolean");
  for (const field of ["archiveSeconds","fieldSeconds","reconstructionSeconds"])
    ensure(Number.isFinite(r[field]) && r[field]>=0 && r[field]<=31536000);
  for (const field of ["firstAttemptCorrect","firstAttemptKnown","answered","attempts","errors",
    "registeredArchiveEvidence","registeredFieldEvidence","questionCount"])
    ensure(Number.isSafeInteger(r[field]) && r[field]>=0 && r[field]<=1000000);
  ensure(Array.isArray(r.questions) && r.questions.length>0 && r.questions.length<=300 &&
    r.questions.length===r.questionCount);
  let answered=0, known=0, correct=0, attempts=0, errors=0;
  const ids=new Set();
  for (const q of r.questions) {
    for (const field of ["caseId","round","id","prompt","explanation"])
      ensure(typeof q[field]==="string" && q[field].length<=12000);
    ensure(q.id.length>0 && q.prompt.length>0 && ["sources","report"].includes(q.round));
    const id=q.caseId+"|"+q.round+"|"+q.id;
    ensure(!ids.has(id)); ids.add(id);
    ensure(Array.isArray(q.answers) && q.answers.length>=2 && q.answers.length<=20 &&
      q.answers.every(a=>typeof a==="string" && a.length>0 && a.length<=6000) &&
      Number.isInteger(q.correctAnswer) && q.correctAnswer>=0 && q.correctAnswer<q.answers.length);
    ensure(Number.isInteger(q.attempts) && q.attempts>=0 && q.attempts<=100000 &&
      Number.isInteger(q.correctResponses) && q.correctResponses>=0 && q.correctResponses<=q.attempts);
    for (const field of ["answered","firstAttemptKnown","firstCorrect","historicalAnswerMismatch"])
      ensure(typeof q[field]==="boolean");
    ensure(Number.isInteger(q.firstAnswer) && q.firstAnswer>=-1 && q.firstAnswer<q.answers.length);
    if(q.firstAttemptKnown) ensure(q.firstAnswer>=0 && q.attempts>0);
    answered+=Number(q.answered); known+=Number(q.firstAttemptKnown);
    correct+=Number(q.firstAttemptKnown && q.firstCorrect);
    attempts+=q.attempts; errors+=q.attempts-q.correctResponses;
  }
  ensure(answered===r.answered && known===r.firstAttemptKnown && correct===r.firstAttemptCorrect &&
    attempts===r.attempts && errors===r.errors);
  return r;
}
async function bodyText(req) {
  if(Number(req.headers.get("content-length"))>550000) throw new RequestError("Arquivo grande demais.",413);
  if(!req.body) throw new RequestError("Relatório ausente.");
  const reader=req.body.getReader(), chunks=[]; let size=0;
  try {
    while(true) {
      const {done,value}=await reader.read(); if(done) break;
      size+=value.length;
      if(size>550000) { await reader.cancel(); throw new RequestError("Arquivo grande demais.",413); }
      chunks.push(value);
    }
  } finally { reader.releaseLock(); }
  const bytes=new Uint8Array(size); let offset=0;
  for(const chunk of chunks) { bytes.set(chunk,offset); offset+=chunk.length; }
  return new TextDecoder("utf-8",{fatal:true}).decode(bytes);
}
Deno.serve(async req => {
  if(req.method==="OPTIONS") return new Response(null,{status:204,headers});
  if(req.method!=="POST") return reply({message:"Use POST para enviar o relatório."},405);
  try {
    if(!req.headers.get("content-type")?.startsWith("application/json"))
      throw new RequestError("Envie JSON.",415);
    let body;
    try { body=JSON.parse(await bodyText(req)); }
    catch(error) { if(error instanceof RequestError) throw error; throw new RequestError("JSON inválido."); }
    const r=await verify(body?.jwt), fingerprint=hex(await digest(body.jwt));
    const url=Deno.env.get("SUPABASE_URL"), secret=Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
    if(!url || !secret) throw new Error("Missing server configuration");
    const dbHeaders={apikey:secret, Authorization:"Bearer "+secret, "Content-Type":"application/json"};
    const row={
      id:r.reportId, identificacao:r.code, nome_aluno:r.studentName,
      concluido_em:r.completedUtc, versao_jogo:r.buildVersion, teste:r.testSession,
      acertos_primeira_tentativa:r.firstAttemptCorrect, perguntas_com_historico:r.firstAttemptKnown,
      perguntas_concluidas:r.answered, total_perguntas:r.questionCount, tentativas:r.attempts, erros:r.errors,
      tempo_acervo_segundos:r.archiveSeconds, tempo_acampamento_segundos:r.fieldSeconds,
      tempo_reconstrucao_segundos:r.reconstructionSeconds,
      evidencias_acervo:r.registeredArchiveEvidence, evidencias_acampamento:r.registeredFieldEvidence,
      historico_incompleto:r.answerHistoryIncomplete, tempo_incompleto:r.fieldTimeIncomplete,
      respostas:r.questions, relatorio:r, assinatura_sha256:fingerprint
    };
    const table=url+"/rest/v1/resultados_dossie";
    const insert=await fetch(table,{method:"POST",headers:{...dbHeaders,Prefer:"return=minimal"},
      body:JSON.stringify(row),signal:AbortSignal.timeout(10000)});
    if(!insert.ok && insert.status!==409) throw new Error("Database insert failed");
    const stored=await fetch(table+"?id=eq."+r.reportId+"&select=id,identificacao,recebido_em,assinatura_sha256",
      {headers:dbHeaders,signal:AbortSignal.timeout(10000)});
    if(!stored.ok) throw new Error("Receipt lookup failed");
    const rows=await stored.json(), receipt=rows[0];
    if(!receipt || receipt.assinatura_sha256!==fingerprint)
      throw new RequestError("Essa identificação já foi recebida com outro conteúdo.",409);
    return reply({ok:true,reportId:receipt.id,code:receipt.identificacao,receivedUtc:receipt.recebido_em});
  } catch(error) {
    if(error instanceof RequestError) return reply({message:error.message},error.status);
    console.error("Report submission failed:",error?.name || "Error");
    return reply({message:"O serviço está indisponível. Tente novamente em instantes."},503);
  }
});
