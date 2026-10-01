export function botDraftOption(enabled:boolean,editable=true){
 return `<label class="settings-row">Skip bot chip choices <input id="skip-bot-drafts" type="checkbox" ${enabled?'checked':''} ${editable?'':'disabled'}/></label><p class="fine">Bots still receive a Chip instantly. Their selection screens and reveals are skipped.</p>`;
}
