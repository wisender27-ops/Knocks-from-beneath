import fs from 'node:fs/promises';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const source = await fs.readFile('DESIGN_ROADMAP.md', 'utf8');
const section = source.split('## 6. План по фазам')[1].split('### Рекомендуемый порядок')[0];
let phase = '';
const rows = [];
for (const line of section.split(/\r?\n/)) {
  if (line.startsWith('### Фаза')) phase = line.replace(/^### /, '').replace(/\s*\(.*$/, '');
  const m = line.match(/^- \[([ x~\-])\] \*\*([A-Z]-\d+[a-z]?)\.\s*(.*?)\*\*(.*)$/);
  if (!m) continue;
  const state = {' ': 'Не начато', 'x': 'Готово', '~': 'В работе', '-': 'Отменено'}[m[1]];
  const owner = /в работе у ([\w-]+)/i.exec(m[4])?.[1] || /\(ENDERWIS\)/i.test(m[4]) && 'ENDERWIS' || '';
  const title = m[3].replace(/[.`]/g, '').trim();
  rows.push([m[2], phase, title, state, owner]);
}
const wb = Workbook.create();
const s = wb.worksheets.add('Roadmap');
s.showGridLines = false;
s.getRange('A1:E1').merge();
s.getRange('A1').values = [['KNOCKS FROM BENEATH • ROADMAP']];
s.getRange('A2:E2').merge();
s.getRange('A2').values = [['Снимок DESIGN_ROADMAP.md на 27.10.2026 • исходный файл остаётся главным планом']];
s.getRange('A4:E4').values = [['Всего', 'Готово', 'В работе', 'Не начато', 'Отменено']];
s.getRange('A5:E5').formulas = [['=COUNTA(A9:A500)', '=COUNTIF(D9:D500,"Готово")', '=COUNTIF(D9:D500,"В работе")', '=COUNTIF(D9:D500,"Не начато")', '=COUNTIF(D9:D500,"Отменено")']];
s.getRange('A7:E7').merge();
s.getRange('A7').values = [['Изменяйте статус в списке; используйте фильтры в заголовках. Таблица не синхронизируется с roadmap автоматически.']];
s.getRange('A8:E8').values = [['ID', 'Фаза', 'Задача', 'Статус', 'Исполнитель']];
s.getRange(`A9:E${8 + rows.length}`).values = rows;
s.tables.add(`A8:E${8 + rows.length}`, true, 'RoadmapTasks');
s.getRange('D9:D500').dataValidation = {rule:{type:'list', values:['Не начато','В работе','Готово','Отменено']}};
for (const [value, color] of [['Готово','#D9EDE1'],['В работе','#FFF0C2'],['Не начато','#E6EDF5'],['Отменено','#F0E2E2']]) {
  s.getRange('D9:D500').conditionalFormats.add('containsText',{text:value,format:{fill:color}});
}
s.freezePanes.freezeRows(8);
s.getRange('A:A').format.columnWidth = 12;
s.getRange('B:B').format.columnWidth = 38;
s.getRange('C:C').format.columnWidth = 46;
s.getRange('D:D').format.columnWidth = 18;
s.getRange('E:E').format.columnWidth = 18;
s.getRange('A1:E1').format.fill = '#17283A';
s.getRange('A1:E1').format.font = {bold:true,color:'#FFFFFF',size:16};
s.getRange('A1:E1').format.rowHeight = 32;
s.getRange('A4:E4').format.fill = '#DCE6EF';
s.getRange('A4:E4').format.font = {bold:true,color:'#243C50'};
s.getRange('A5:E5').format.font = {bold:true,size:15,color:'#1D4962'};
s.getRange('A8:E8').format.font = {bold:true,color:'#FFFFFF'};
s.getRange('A2:E2').format.font = {color:'#536777',size:10};
s.getRange('A7:E7').format.font = {color:'#536777',size:10};
s.getRange(`A9:E${8+rows.length}`).format.rowHeight = 21;
wb.recalculate();
await fs.mkdir('outputs/roadmap', {recursive:true});
const preview = await wb.render({sheetName:'Roadmap',range:'A1:E20',scale:1,format:'png'});
await fs.writeFile('outputs/roadmap/preview.png', new Uint8Array(await preview.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(wb)).save('outputs/roadmap/Roadmap.xlsx');
console.log(JSON.stringify({count:rows.length, states:rows.reduce((o,r)=>(o[r[3]]=(o[r[3]]||0)+1,o),{}), sample:rows.slice(0,3)}));
