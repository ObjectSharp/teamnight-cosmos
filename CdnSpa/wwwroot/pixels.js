
new Vue({
    el: '#app',
    data: {
        image: {},
        inputFile: {selected: false },
        inputCanvas: {},
        inputCtx: {},
        inputChecksum: "",
        inputChunkSize: 100,
        inputRandomizeRegion: false,
        inputName: "",

        sending: false,
        totalSent: 0,
        start: null,
        stop: null,

        eastSentCtx: {},
        eastSentCanvas: {},
        eastCtx: {},
        eastCanvas: {},
        eastChecksum: "",
        eastGood: false,
        eastBuffer: [],

        centralSentCtx: {},
        centralSentCanvas: {},
        centralCtx: {},
        centralCanvas: {},
        centralChecksum: "",
        centralGood: false,
        centralBuffer: [],

        westSentCtx: {},
        westSentCanvas: {},
        westCtx: {},
        westCanvas: {},
        westChecksum: "",
        westGood: false,
        westBuffer: [],
    },
    methods: {
        refreshEast: function () {
            this.eastGood = false;
            axios.get('http://speedtestapieast.azurewebsites.net/api/pixel/' + this.inputFile.name)
                .then(response => {
                    var d = response.data;
                    this.eastCount = d.count;
                    for (var n = 0; n < d.data.length; n++) {
                        var ids = this.eastCtx.createImageData(1, 1);
                        ids.data[0] = d.data[n].r;
                        ids.data[1] = d.data[n].g;
                        ids.data[2] = d.data[n].b;
                        ids.data[3] = d.data[n].a;
                        this.eastCtx.putImageData(ids, d.data[n].x, d.data[n].y);
                    }
                    this.inputChecksum = this.inputCanvas.toDataURL("image/png");
                    this.eastChecksum = this.eastCanvas.toDataURL("image/png");
                    this.eastGood = this.inputChecksum == this.eastChecksum;
                });
            },
        refreshCentral: function () {
            this.centralGood = false;
            axios.get('http://speedtestapicentral.azurewebsites.net/api/pixel/' + this.inputFile.name)
                .then(response => {
                    var d = response.data;
                    this.centralCount = d.count;
                    for (var n = 0; n < d.data.length; n++) {
                        var ids = this.centralCtx.createImageData(1, 1);
                        ids.data[0] = d.data[n].r;
                        ids.data[1] = d.data[n].g;
                        ids.data[2] = d.data[n].b;
                        ids.data[3] = d.data[n].a;
                        this.centralCtx.putImageData(ids, d.data[n].x, d.data[n].y);
                    }
                    this.inputChecksum = this.inputCanvas.toDataURL("image/png");
                    this.centralChecksum = this.centralCanvas.toDataURL("image/png");
                    this.centralGood = this.inputChecksum == this.centralChecksum;
                });
            },
        refreshWest: function () {
            this.westGood = false;
            axios.get('http://speedtestapiwest.azurewebsites.net/api/pixel/' + this.inputFile.name)
                .then(response => {
                    var d = response.data;
                    this.westCount = d.count;
                    for (var n = 0; n < d.data.length; n++) {
                        var ids = this.westCtx.createImageData(1, 1);
                        ids.data[0] = d.data[n].r;
                        ids.data[1] = d.data[n].g;
                        ids.data[2] = d.data[n].b;
                        ids.data[3] = d.data[n].a;
                        this.westCtx.putImageData(ids, d.data[n].x, d.data[n].y);
                    }
                    this.inputChecksum = this.inputCanvas.toDataURL("image/png");
                    this.westChecksum = this.westCanvas.toDataURL("image/png");
                    this.westGood = this.inputChecksum == this.westChecksum;
                });
        },
        popAndSend: function (region) {
            var vue = this;
            
            var sentCtx = {};
            var buffer = [];
            var url = "";

            if (region === "east") {
                url = "http://speedtestapieast.azurewebsites.net";
                buffer = vue.eastBuffer;
                sentCtx = vue.eastSentCtx;
            } else if (region === "central") {
                url = "http://speedtestapicentral.azurewebsites.net";
                buffer = vue.centralBuffer;
                sentCtx = vue.centralSentCtx;
            } else if (region === "west") {
                url = "http://speedtestapiwest.azurewebsites.net";
                buffer = vue.westBuffer;
                sentCtx = vue.westSentCtx;
            } else {
                return;
            }
            
            var toSend = buffer.slice(0, vue.inputChunkSize);
            if (toSend.length <= 0) {
                return;
            } else if (toSend.length == 1) {
                axios.post(url + '/api/pixel', toSend[0])
                    .then(response => {
                        vue.totalSent++;
                        var x = response.data.data.x;
                        var y = response.data.data.y;
                        var id = vue.inputCtx.getImageData(x, y, 1, 1);
                        sentCtx.putImageData(id, x, y);
                        vue.eastCtx.putImageData(id, x, y);
                        vue.centralCtx.putImageData(id, x, y);
                        vue.westCtx.putImageData(id, x, y);
                        if (vue.totalSent >= vue.image.width * vue.image.height) {
                            vue.refreshEast();
                            vue.refreshCentral();
                            vue.refreshWest();
                            vue.sending = false;
                        }
                        buffer.shift();
                        vue.popAndSend(region);
                    })
                    .catch(error => {
                        console.log(error);
                        vue.popAndSend(region);
                    });
            } else {
                axios.post(url + '/api/pixel/bulk2', toSend)
                    .then(response => {
                        vue.totalSent += response.data.count;
                        for (var e = 0; e < response.data.seqs.length; e++) {
                            var x = response.data.seqs[e].x;
                            var y = response.data.seqs[e].y;
                            var id = vue.inputCtx.getImageData(x, y, 1, 1);
                            sentCtx.putImageData(id, x, y);
                            vue.eastCtx.putImageData(id, x, y);
                            vue.centralCtx.putImageData(id, x, y);
                            vue.westCtx.putImageData(id, x, y);
                            buffer.shift();
                        }
                        if (vue.totalSent >= vue.image.width * vue.image.height) {
                            vue.refreshEast();
                            vue.refreshCentral();
                            vue.refreshWest();
                            vue.sending = false;
                        }
                        vue.popAndSend(region);
                    })
                    .catch(error => {
                        console.log(error);
                        vue.popAndSend(region);
                    });
            }
        },
        sendPixelData: function () {
            var n = 0;
            var vue = this;
            vue.sending = true;
            vue.pixels = [];
            vue.sentCount = 0;
            for (var y = 0; y < vue.image.height; y++) {
                for (var x = 0; x < vue.image.width; x++) {
                    var id = vue.inputCtx.getImageData(x, y, 1, 1);
                    var p = { name: vue.inputName, x: x, y: y, r: id.data[0], g: id.data[1], b: id.data[2], a: id.data[3], seq: n++ };
                    vue.pixels.push(p);
                    if (vue.inputRandomizeRegion === false)
                        vue.eastBuffer.push(p);
                    else {
                        var region = Math.round(Math.random() * 2) + 1;
                        if (region === 1)
                            vue.eastBuffer.push(p);
                        else if (region === 2)
                            vue.centralBuffer.push(p);
                        else
                            vue.westBuffer.push(p);
                    }
                }
            }

            if (vue.inputRandomizeRegion === false) {
                vue.popAndSend("east");
            } else {
                vue.popAndSend("east");
                vue.popAndSend("central");
                vue.popAndSend("west");
            }
        },
        processFile: function (event) {
            var vue = this;
            var f = event.target.files[0];

            // Only process image files.
            if (!f.type.match('image.*')) {
                this.inputFile = { selected: false };
                return;
            }

            this.inputFile = f;
            this.inputFile.selected = true;

            var reader = new FileReader();
            reader.onload = renderImage;
            // Read in the image file as a data URL.
            reader.readAsDataURL(f);

            vue.inputName = f.name;

            function renderImage(e) {
                var img = new Image();
                img.onload = renderCanvas;
                img.src = reader.result;
                vue.image = img;
            }

            function renderCanvas(e) {
                var w = vue.image.width;
                var h = vue.image.height;
                var inputCanvas = document.getElementById("inputCanvas");
                inputCanvas.width = w;
                inputCanvas.height = h;
                inputCtx = inputCanvas.getContext("2d");
                inputCtx.drawImage(vue.image, 0, 0);
                vue.inputCtx = inputCtx;
                vue.inputCanvas = inputCanvas;
                vue.inputChunkSize = w;

                vue.eastSentCanvas = document.getElementById("eastSentCanvas");
                vue.eastSentCanvas.width = w;
                vue.eastSentCanvas.height = h;
                vue.eastSentCtx = vue.eastSentCanvas.getContext("2d");
                vue.eastCanvas = document.getElementById("eastCanvas");
                vue.eastCanvas.width = w;
                vue.eastCanvas.height = h;
                vue.eastCtx = vue.eastCanvas.getContext("2d");

                vue.centralSentCanvas = document.getElementById("centralSentCanvas");
                vue.centralSentCanvas.width = w;
                vue.centralSentCanvas.height = h;
                vue.centralSentCtx = vue.centralSentCanvas.getContext("2d");
                vue.centralCanvas = document.getElementById("centralCanvas");
                vue.centralCanvas.width = w;
                vue.centralCanvas.height = h;
                vue.centralCtx = vue.centralCanvas.getContext("2d");

                vue.westSentCanvas = document.getElementById("westSentCanvas");
                vue.westSentCanvas.width = w;
                vue.westSentCanvas.height = h;
                vue.westSentCtx = vue.westSentCanvas.getContext("2d");
                vue.westCanvas = document.getElementById("westCanvas");
                vue.westCanvas.width = w;
                vue.westCanvas.height = h;
                vue.westCtx = vue.westCanvas.getContext("2d");
            }
        }
    }
});

function initCanvas() {
    var w = 50;
    var h = 50;
    var inputCanvas = document.getElementById("inputCanvas");
    inputCanvas.width = w;
    inputCanvas.height = h;
    

    var eastSentCanvas = document.getElementById("eastSentCanvas");
    eastSentCanvas.width = w;
    eastSentCanvas.height = h;
    var eastCanvas = document.getElementById("eastCanvas");
    eastCanvas.width = w;
    eastCanvas.height = h;

    var centralSentCanvas = document.getElementById("centralSentCanvas");
    centralSentCanvas.width = w;
    centralSentCanvas.height = h;
    var centralCanvas = document.getElementById("centralCanvas");
    centralCanvas.width = w;
    centralCanvas.height = h;

    var westSentCanvas = document.getElementById("westSentCanvas");
    westSentCanvas.width = w;
    westSentCanvas.height = h;
    var westCanvas = document.getElementById("westCanvas");
    westCanvas.width = w;
    westCanvas.height = h;
}
        
    