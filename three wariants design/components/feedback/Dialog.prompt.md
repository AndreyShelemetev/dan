Модальное окно — подтверждения (принять смету, согласовать допработу).

```jsx
<Dialog open={open} onClose={close} title="Принять смету?"
  footer={<><Button variant="secondary" onClick={close}>Назад</Button><Button onClick={accept}>Принять версию 2</Button></>}>
  После принятия изменение строк потребует повторного согласия.
</Dialog>
```
