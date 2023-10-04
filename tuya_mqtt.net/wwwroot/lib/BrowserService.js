// functions for BrowserService.cs

export function scrollToElement(elem,index) {
    $(elem).get(index).scrollIntoView()
}

export function timeZoneOffset()  {
    return new Date().getTimezoneOffset();
}

export function getBrowserLanguage() {
    return (navigator.languages && navigator.languages.length) ? navigator.languages[0] :
        navigator.userLanguage || navigator.language || navigator.browserLanguage || 'en';
}

export function downloadFileFromStream(fileName, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
    URL.revokeObjectURL(url);
}