const colours = ["#ff2790", "#bf1d6c", "#7f1348", "#3f0924"];
const hexGrid = document.getElementById("hex-grid");
const hexSize = 100; 

function RandomColour() {
  return colours[Math.floor(Math.random() * colours.length)];
}

function HexGrid() {
  document.documentElement.style.setProperty("--hex-size", `${hexSize}px`);
  const hexHeight = hexSize * Math.cos(30*(Math.PI/180));
  const vw = window.innerWidth;
  const vh = window.innerHeight;

  const cols = Math.ceil(vw / (hexSize * 0.75))+1;
  const rows = Math.ceil(vh / hexHeight)+1;

  hexGrid.style.transform = `translate(${(vw - (cols * hexSize * 0.75 + hexSize * 0.25)) / 2}px, ${(vh - (rows * hexHeight + hexHeight / 2)) / 2}px)`;

  for (let row = 0; row < rows; row++) {
    for (let col = 0; col < cols; col++) {
      const hex = document.createElement("div");
      hex.classList.add("hex");

      const hexInner = document.createElement("div");
      hexInner.classList.add("hex-inner");
      hexInner.style.backgroundColor = RandomColour();

      hex.appendChild(hexInner);

      const xOffset = col * hexSize * 0.75;
      const yOffset = row * hexHeight + (col % 2 === 0 ? 0 : hexHeight / 2);

      hex.style.transform = `translate(${xOffset}px, ${yOffset}px)`;

      hexGrid.appendChild(hex);
      animateHex(hexInner);
    }
  }
}

function animateHex(hexInner) {
  const delay = Math.random() * 8000;
  setTimeout(() => {
    hexInner.classList.add("hidden");
    setTimeout(() => {
      hexInner.style.backgroundColor = RandomColour();
      hexInner.classList.remove("hidden");
      animateHex(hexInner);
    }, 2000);
  }, delay);
}

function getDetailedSize(element) {
  const rect = element.getBoundingClientRect();
  return {
    width: rect.width,
    height: rect.height,
    visibleWidth: element.offsetWidth,
    visibleHeight: element.offsetHeight,
  };
}

window.addEventListener("resize", HexGrid);
HexGrid();

const myElement = document.getElementById("hex-grid");
const detailedSize = getDetailedSize(myElement);
console.log(detailedSize);