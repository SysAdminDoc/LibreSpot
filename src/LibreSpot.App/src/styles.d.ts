declare module "*.css";

declare module "*.txt" {
  const text: string;
  export default text;
}

declare module "*.svg" {
  const text: string;
  export default text;
}

declare module "*.png" {
  const dataUrl: string;
  export default dataUrl;
}
