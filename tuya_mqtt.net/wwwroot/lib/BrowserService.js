// functions for BrowserService.cs

// listen for page resize
export function resizeListener(dotnethelper) {
    $(window).on("resize", function () {
        let browserHeight = $(window).innerHeight();
        let browserWidth = $(window).innerWidth();
        dotnethelper.invokeMethodAsync('SetBrowserDimensions', browserWidth, browserHeight).then(() => {
            // success, do nothing
        }).catch(error => {
            console.log("Error during browser resize: " + error);
        });
    });
}

export function viewportSize() {
    let Height = $(window).innerHeight();
    let Width = $(window).innerWidth();
    return [Width, Height];
}

//get dimensions of a HTML object
export function MyGetBoundingClientRect(element)
{
    return element.getBoundingClientRect().catch(error => {
        console.log("Error retrieving getBoundingClientRect: " + error);
    });
}


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