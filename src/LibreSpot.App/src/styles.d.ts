declare module "*.css";

declare module "*.txt" {
  const text: string;
  export default text;
}
