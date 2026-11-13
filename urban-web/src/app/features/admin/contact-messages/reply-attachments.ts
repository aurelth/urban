/** Limites do texto da resposta, os mesmos da API. */
export const REPLY_BODY_LIMITS = { min: 5, max: 5000 } as const;

const KB = 1024;
const MB = 1024 * KB;

/** Limites dos anexos, os mesmos da API (10 MB no total é o que cabe no e-mail da Brevo depois do base64). */
export const ATTACHMENT_LIMITS = {
  maxFiles: 3,
  maxFileBytes: 5 * MB,
  maxTotalBytes: 10 * MB,
} as const;

const EXTENSIONS = ['pdf', 'png', 'jpg', 'jpeg', 'docx', 'xlsx', 'csv', 'txt'] as const;

/** Valor do atributo `accept` do seletor de arquivos: só ajuda a escolher, quem decide é a API (confere o conteúdo). */
export const ATTACHMENT_ACCEPT = EXTENSIONS.map((extension) => `.${extension}`).join(',');

const NUMBER = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1, useGrouping: false });

/** "850 B", "1,5 KB", "2,5 MB". */
export function formatFileSize(bytes: number): string {
  if (bytes < KB) {
    return `${bytes} B`;
  }
  return bytes < MB ? `${NUMBER.format(bytes / KB)} KB` : `${NUMBER.format(bytes / MB)} MB`;
}

/** Explicação dos limites, mostrada embaixo do botão de anexar. */
export const ATTACHMENT_HINT =
  `Até ${ATTACHMENT_LIMITS.maxFiles} arquivos de ${formatFileSize(ATTACHMENT_LIMITS.maxFileBytes)}, ` +
  `com no máximo ${formatFileSize(ATTACHMENT_LIMITS.maxTotalBytes)} no total. ` +
  'Aceitamos PDF, PNG, JPG, DOCX, XLSX, CSV e TXT';

export interface AttachmentCheck {
  /** A lista que vale depois da escolha: os que já estavam e os novos que servem. */
  readonly files: readonly File[];
  /** Um aviso por arquivo recusado, mais os de quantidade e de soma, em texto para a tela. */
  readonly problems: readonly string[];
}

/** Acrescenta os arquivos escolhidos aos que já estavam, recusando os que a API recusaria. */
export function checkAttachments(
  current: readonly File[],
  picked: readonly File[],
): AttachmentCheck {
  const files = [...current];
  const problems: string[] = [];
  let total = files.reduce((sum, file) => sum + file.size, 0);
  let tooMany = false;
  let tooBig = false;

  for (const file of picked) {
    const refusal = refusalOf(file);
    if (refusal !== null) {
      problems.push(`“${file.name}”: ${refusal}`);
    } else if (files.length >= ATTACHMENT_LIMITS.maxFiles) {
      tooMany = true;
    } else if (total + file.size > ATTACHMENT_LIMITS.maxTotalBytes) {
      tooBig = true;
    } else {
      files.push(file);
      total += file.size;
    }
  }

  if (tooMany) {
    problems.push(`Anexe no máximo ${ATTACHMENT_LIMITS.maxFiles} arquivos`);
  }
  if (tooBig) {
    problems.push(`Os anexos somam mais de ${formatFileSize(ATTACHMENT_LIMITS.maxTotalBytes)}`);
  }
  return { files, problems };
}

function refusalOf(file: File): string | null {
  if (!hasAllowedExtension(file.name)) {
    return 'tipo de arquivo não aceito';
  }
  if (file.size === 0) {
    return 'o arquivo está vazio';
  }
  if (file.size > ATTACHMENT_LIMITS.maxFileBytes) {
    return `passa de ${formatFileSize(ATTACHMENT_LIMITS.maxFileBytes)}`;
  }
  return null;
}

function hasAllowedExtension(name: string): boolean {
  const dot = name.lastIndexOf('.');
  return dot > 0 && (EXTENSIONS as readonly string[]).includes(name.slice(dot + 1).toLowerCase());
}
