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
    return element.getBoundingClientRect();
}


export function scrollToElement(elem,index) {
    $(elem).get(index).scrollIntoView()
}

