// WMS Operations Utility Algorithms

export interface Gs1Result {
  gtin: string | null;
  expiryDate: string | null;
  lot: string | null;
  quantity: number | null;
  raw: string;
  validationError: string | null;
}

/**
 * Analisa uma string GS1 pura e extrai os Identificadores de Aplicação (AIs)
 * Suporta AIs comuns: (01) GTIN, (10) Lote, (17) Validade, (37) Quantidade
 * @param rawBarcode - Conteúdo decodificado pelo scanner (ex: "01056012345678901727031510LOTE12345\x1D370150")
 */
export function parseGS1Barcode(rawBarcode: string): Gs1Result {
  const result: Gs1Result = {
    gtin: null,
    expiryDate: null,
    lot: null,
    quantity: null,
    raw: rawBarcode,
    validationError: null
  };

  let i = 0;
  while (i < rawBarcode.length) {
    const ai2 = rawBarcode.substring(i, i + 2);
    
    // Verificar AI (01) - GTIN (Comprimento Fixo: 14 dígitos)
    if (ai2 === "01") {
      result.gtin = rawBarcode.substring(i + 2, i + 16);
      i += 16;
    }
    // Verificar AI (17) - Data de Validade (Comprimento Fixo: 6 dígitos em formato YYMMDD)
    else if (ai2 === "17") {
      const dateStr = rawBarcode.substring(i + 2, i + 8);
      const year = "20" + dateStr.substring(0, 2);
      const month = dateStr.substring(2, 4);
      const day = dateStr.substring(4, 6);

      // Regra Crítica de Janeiro de 2025: Proibido dia "00" no padrão GS1
      if (day === "00") {
        result.validationError = "Erro Regulatório GS1: Data de validade não pode conter o dia 00. Introduza o dia manualmente.";
      }

      result.expiryDate = `${year}-${month}-${day}`;
      i += 8;
    }
    // Verificar AI (10) - Número de Lote (Comprimento Variável até 20 carateres, delimitado por \x1D)
    else if (ai2 === "10") {
      let lotData = "";
      let j = i + 2;
      while (j < rawBarcode.length && rawBarcode.charAt(j) !== "\x1D" && rawBarcode.charAt(j) !== "\u001D") {
        lotData += rawBarcode.charAt(j);
        j++;
      }
      result.lot = lotData;
      // Avança a posição do ponteiro considerando se encontrou o delimitador
      const charAtJ = rawBarcode.charAt(j);
      i = (j < rawBarcode.length && (charAtJ === "\x1D" || charAtJ === "\u001D")) ? j + 1 : j;
    }
    // Verificar AI (37) - Quantidade (Comprimento Variável, delimitado por \x1D)
    else if (ai2 === "37") {
      let qtyData = "";
      let j = i + 2;
      while (j < rawBarcode.length && rawBarcode.charAt(j) !== "\x1D" && rawBarcode.charAt(j) !== "\u001D") {
        qtyData += rawBarcode.charAt(j);
        j++;
      }
      result.quantity = parseInt(qtyData, 10);
      const charAtJ = rawBarcode.charAt(j);
      i = (j < rawBarcode.length && (charAtJ === "\x1D" || charAtJ === "\u001D")) ? j + 1 : j;
    }
    else {
      // Ignora carateres não mapeados ou avança para evitar loops infinitos
      i++;
    }
  }

  return result;
}

export interface LocationCoordinates {
  corredor: string;
  estante: string;
  prateleira: string;
  alveolo: string;
}

export interface PickingTaskLine {
  id: string;
  ref: string;
  designacao: string;
  quantidade: number;
  localizacao: string;
  lote?: string | null;
  // Localização estendida
  localizacaoCoords?: LocationCoordinates;
}

/**
 * Ordena as tarefas de picking para minimizar a distância percorrida pelo operador
 * Baseado no mapeamento do corredor e alvéolo físico (Heurística S-Shape / Serpentina)
 */
export function optimizeS_ShapePath(pickingTasks: PickingTaskLine[]): PickingTaskLine[] {
  return [...pickingTasks].sort((a, b) => {
    const coordsA = a.localizacaoCoords || parseLocationName(a.localizacao);
    const coordsB = b.localizacaoCoords || parseLocationName(b.localizacao);

    const corredorA = parseInt(coordsA.corredor, 10) || 1;
    const corredorB = parseInt(coordsB.corredor, 10) || 1;

    if (corredorA !== corredorB) {
      return corredorA - corredorB;
    }

    const estanteA = parseInt(coordsA.estante, 10) || 1;
    const estanteB = parseInt(coordsB.estante, 10) || 1;

    if (corredorA % 2 === 1) {
      // Corredor Ímpar: Sentido ascendente (da estante 1 para a estante N)
      return estanteA - estanteB;
    } else {
      // Corredor Par: Sentido descendente (da estante N para a estante 1)
      return estanteB - estanteA;
    }
  });
}

/**
 * Utilitário para deduzir corredor e estante a partir de formatos comuns de localização (ex: "A-02-04-01")
 */
function parseLocationName(locName: string): LocationCoordinates {
  // Exemplo de formato: "01-02-03-04" ou "A-01-02"
  const parts = locName.split("-");
  if (parts.length >= 2) {
    // Tenta encontrar o corredor e estante no nome
    // Se a primeira parte for uma letra, ex "A", usamos a segunda parte como corredor
    const isFirstLetter = isNaN(parseInt(parts[0], 10));
    const corredor = isFirstLetter ? parts[1] : parts[0];
    const estante = isFirstLetter ? (parts[2] || "1") : (parts[1] || "1");
    return {
      corredor: corredor || "1",
      estante: estante || "1",
      prateleira: "1",
      alveolo: "1"
    };
  }
  
  // Se for "GERAL" ou "CQ" ou formato desconhecido
  return {
    corredor: "1",
    estante: "1",
    prateleira: "1",
    alveolo: "1"
  };
}

export interface PackedItem {
  ref: string;
  quantidade: number;
  peso_unitario_kg: number;
}

export interface WeightValidationResult {
  isValid: boolean;
  theoreticalWeight: number;
  minAllowed: number;
  maxAllowed: number;
  deviation: number;
}

/**
 * Valida a integridade física do volume pesando-o contra o peso teórico de cadastro
 * @param actualWeight - Peso lido diretamente pela balança
 * @param packedItems - Lista de itens contidos no volume com quantidade e peso unitário
 * @param boxTare - Peso padrão da caixa de embalagem
 * @param tolerancePercent - Margem aceitável de desvio (padrão 2.0 para 2%)
 */
export function validatePackingWeight(
  actualWeight: number,
  packedItems: PackedItem[],
  boxTare: number,
  tolerancePercent = 2.0
): WeightValidationResult {
  let theoreticalWeight = boxTare;

  packedItems.forEach(item => {
    theoreticalWeight += (item.quantidade * item.peso_unitario_kg);
  });

  const maxDeviation = theoreticalWeight * (tolerancePercent / 100);
  const minAllowed = theoreticalWeight - maxDeviation;
  const maxAllowed = theoreticalWeight + maxDeviation;

  const isValid = actualWeight >= minAllowed && actualWeight <= maxAllowed;

  return {
    isValid: isValid,
    theoreticalWeight: parseFloat(theoreticalWeight.toFixed(3)),
    minAllowed: parseFloat(minAllowed.toFixed(3)),
    maxAllowed: parseFloat(maxAllowed.toFixed(3)),
    deviation: parseFloat((actualWeight - theoreticalWeight).toFixed(3))
  };
}

// Web Serial Interfaces
let serialPort: any = null;
let serialReader: any = null;

/**
 * Inicia a escuta da porta série selecionada para obter o peso em tempo real
 * @param onWeightReceived - Callback que recebe o peso em Kg (float)
 * @param onError - Callback opcional para tratamento de erros de ligação
 */
export async function connectToScale(
  onWeightReceived: (weight: number) => void,
  onError?: (err: any) => void
): Promise<void> {
  try {
    const nav = navigator as any;
    if (!("serial" in nav)) {
      alert("A Web Serial API não é suportada por este browser. Utilize o Chrome ou Edge.");
      return;
    }

    // Solicita seleção da porta de hardware ao utilizador
    serialPort = await nav.serial.requestPort();
    
    // Parâmetros universais RS232: 9600 Baud, 8 bits de dados, 1 stop bit, sem paridade
    await serialPort.open({ baudRate: 9600, dataBits: 8, stopBits: 1, parity: "none" });
    
    // Em navegadores modernos, podemos usar TextDecoderStream
    const textDecoder = new TextDecoderStream();
    serialPort.readable.pipeTo(textDecoder.writable);
    serialReader = textDecoder.readable.getReader();

    let stringBuffer = "";

    // Loop contínuo de leitura de dados do stream
    while (true) {
      const { value, done } = await serialReader.read();
      if (done) {
        break;
      }
      if (value) {
        stringBuffer += value;
        
        // As balanças terminam cada transmissão de linha de dados com '\r' ou '\n'
        if (stringBuffer.includes("\n")) {
          const lines = stringBuffer.split("\n");
          // Processa a penúltima linha que está garantidamente completa
          const completeLine = lines[lines.length - 2];
          
          // Regex para capturar números decimais presentes no sinal da balança (ex: "ST,GS,  12.450,kg")
          const weightMatch = completeLine.match(/[-+]?\d*\.\d+|\d+/);
          if (weightMatch) {
            const currentWeight = parseFloat(weightMatch[0]);
            onWeightReceived(currentWeight);
          }
          stringBuffer = lines[lines.length - 1]; // Mantém restos incompletos no buffer
        }
      }
    }
  } catch (error) {
    console.error("Erro na comunicação série com a balança:", error);
    if (onError) {
      onError(error);
    }
  }
}

/**
 * Encerra a ligação série e liberta os recursos do browser
 */
export async function disconnectScale(): Promise<void> {
  if (serialReader) {
    try {
      await serialReader.cancel();
    } catch (e) {
      console.warn("Error cancelling serial reader:", e);
    }
    serialReader = null;
  }
  if (serialPort) {
    try {
      await serialPort.close();
    } catch (e) {
      console.warn("Error closing serial port:", e);
    }
    serialPort = null;
  }
}
